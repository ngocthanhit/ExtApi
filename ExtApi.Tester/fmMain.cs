﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ExtApi.Engine.Data;
using ExtApi.Engine;
using System.IO;
using ExtApi.Tester.Properties;
using Newtonsoft.Json;

namespace ExtApi.Tester
{
    public partial class fmMain : Form
    {
        protected string _currentFileName;
        protected bool _dataModified;

        public fmMain()
        {
            InitializeComponent();
        }

        private void fmMain_Load(object sender, EventArgs e)
        {
            string lastApiFile = Settings.Default.LastApiFile;
            if (!string.IsNullOrWhiteSpace(lastApiFile))
                OpenApiCall(lastApiFile);

            _dataModified = false;
            UpdateTitle();
        }

        private void btnAddParameter_Click(object sender, EventArgs e)
        {
            var editor = new fmParameterEditor(new ApiParameter());
            if (editor.ShowDialog() == DialogResult.OK)
            {
                SetDataModified();
                lstParameters.Items.Add(editor.EditedParameter);
            }
        }

        private void btnEditParam_Click(object sender, EventArgs e)
        {
            if (lstParameters.SelectedItem == null)
                return;

            var editor = new fmParameterEditor((ApiParameter)lstParameters.SelectedItem);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                int index = lstParameters.SelectedIndex;
                lstParameters.Items[index] = editor.EditedParameter;
                SetDataModified();
            }
        }

        private void btnRemoveParameter_Click(object sender, EventArgs e)
        {
            if (lstParameters.SelectedItem == null)
                return;

            lstParameters.Items.Remove(lstParameters.SelectedItem);
            SetDataModified();
        }

        private void chkIncludeOAuth_CheckedChanged(object sender, EventArgs e)
        {
            txtConsumerKey.Enabled = chkIncludeOAuth.Checked;
            txtConsumerSecret.Enabled = chkIncludeOAuth.Checked;
            txtAccessToken.Enabled = chkIncludeOAuth.Checked;
            txtTokenSecret.Enabled = chkIncludeOAuth.Checked;
            SetDataModified();
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            // Reset the GUI
            lblStatusCode.Text = string.Empty;
            txtResults.Text = string.Empty;
            txtBuiltUrl.Text = string.Empty;

            // Get the request method
            RequestMethod selectedMethod = GetRequestMethod();

            // Create parameter list
            var paramList = CreateParameterList();

            ExtApiCallResult result = null;
            var apiRunner = new ApiRunner();

            if (chkIncludeOAuth.Checked)
            {
                // Create the token manager
                var tokenManager = new InMemoryTokenManager();
                tokenManager.ConsumerKey = txtConsumerKey.Text;
                tokenManager.ConsumerSecret = txtConsumerSecret.Text;
                tokenManager.AddKeyAndSecret(txtAccessToken.Text, txtTokenSecret.Text);

                // Call the api
                try 
                {
                    result = apiRunner.ExecuteOAuthApiCall(txtApiUrl.Text, paramList, GetRequestMethod(), txtConsumerKey.Text,
                                                            txtConsumerSecret.Text, txtAccessToken.Text, txtTokenSecret.Text);
                }
                catch (UriFormatException ex)
                {
                    HandleApiCallException(ex);
                    return;
                }
            }

            else
            {
                string username = chkUseWebAuth.Checked ? txtWebAuthUsername.Text : string.Empty;
                string password = chkUseWebAuth.Checked ? txtWebAuthPassword.Text : string.Empty;

                try { result = apiRunner.ExecuteApiCall(txtApiUrl.Text, paramList, selectedMethod, username, password); }
                catch (UriFormatException ex)
                {
                    HandleApiCallException(ex);
                    return;
                }
            }

            // Show the results
            lblStatusCode.Text = result.StatusCode.ToString();
            txtBuiltUrl.Text = result.FinalUrl;

            if (result.XmlResponse != null)
                txtResults.Text = result.XmlResponse.ToString();

            else
            {
                // Check if it's json, if so pretty print it
                var response = new StreamReader(result.ResponseStream).ReadToEnd();

                if (response.Length > 0 && (response[0] == '{' || response[0] == '['))
                    txtResults.Text = JsonFormatter.PrettyPrint(response);
                else
                    txtResults.Text = response;
            }

            // Make sure we don't leak the stream
            result.ResponseStream.Dispose();
        }

        private void chkUseWindowsAuth_CheckedChanged(object sender, EventArgs e)
        {
            txtWebAuthPassword.Enabled = chkUseWebAuth.Checked;
            txtWebAuthUsername.Enabled = chkUseWebAuth.Checked;
        }

        private void HandleApiCallException(Exception ex)
        {
            MessageBox.Show(
                string.Format("An exception occurred while executing the api call {0}{0}{1}:{0}{2}",
                    Environment.NewLine,
                    ex.GetType().ToString(),
                    ex.Message),
                "Error Performing Api Call");
        }

        private void DataModifiedEvent(object sender, EventArgs e)
        {
            SetDataModified();
        }

        private void saveAPICallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentFileName == null)
                saveAsToolStripMenuItem_Click(sender, e);

            else
                SaveApiCall(_currentFileName);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "saved api calls (*.api)|*.api";

            if (dlg.ShowDialog() == DialogResult.OK)
                SaveApiCall(dlg.FileName);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_dataModified)
            {
                var result = MessageBox.Show("The current api call hasn't been saved.  Would you like to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                    return;
            }

            var dlg = new OpenFileDialog();
            dlg.Filter = "saved api calls (*.api)|*.api";

            if (dlg.ShowDialog() == DialogResult.OK)
                OpenApiCall(dlg.FileName);
        }

        private void fmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_dataModified)
            {
                var result = MessageBox.Show("The current api call hasn't been saved.  Would you like to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void newStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (_dataModified)
            {
                var result = MessageBox.Show("The current api call hasn't been saved.  Would you like to continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                    return;
            }

            _currentFileName = null;
            _dataModified = false;
            SetEditorControls(null);
            UpdateTitle();

            Settings.Default.LastApiFile = string.Empty;
            Settings.Default.Save();
        }

        private void SetDataModified()
        {
            _dataModified = true;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            const string title = "ExtApi Tester";

            if (string.IsNullOrWhiteSpace(_currentFileName))
                this.Text = string.Concat(title, " - ", "(Unsaved Api Call)");
            else
                this.Text = string.Concat(title, " - ", _currentFileName);

            if (_dataModified)
                this.Text += " (Modified)";
        }

        private void SaveApiCall(string filename)
        {
            var settings = CreateApiSettings();

            // Save the settings as JSON
            string jsonString = JsonConvert.SerializeObject(settings);

            try
            {
                var stream = File.CreateText(filename);
                stream.Write(jsonString);
                stream.Close();
            }

            catch (IOException ex)
            {
                MessageBox.Show("An error occurred while saving the API call: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _dataModified = false;
            _currentFileName = filename;
            UpdateTitle();

            Settings.Default.LastApiFile = _currentFileName;
            Settings.Default.Save();
        }

        private void OpenApiCall(string filename)
        {
            // Read the json from the selected file
            StreamReader stream;
            ExtApiSettings settings;

            try { stream = File.OpenText(filename); }
            catch (IOException ex)
            {
                MessageBox.Show("An error occurred while opening up the api call: " + ex.Message, "Error Opening Api", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try { settings = JsonConvert.DeserializeObject<ExtApiSettings>(stream.ReadToEnd()); }
            catch (JsonReaderException)
            {
                MessageBox.Show("The selected file is not a valid saved API call", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            finally
            {
                stream.Close();
            }

            if (settings != null)
            {
                SetEditorControls(settings);
                _dataModified = false;
                _currentFileName = filename;
                UpdateTitle();
            }

            Settings.Default.LastApiFile = _currentFileName;
            Settings.Default.Save();
        }

        private ExtApiSettings CreateApiSettings()
        {
            var settings = new ExtApiSettings();
            settings.LastApiUrl = txtApiUrl.Text;
            settings.LastOAuthAccessToken = txtAccessToken.Text;
            settings.LastOAuthConsumerKey = txtConsumerKey.Text;
            settings.LastOAuthConsumerSecret = txtConsumerSecret.Text;
            settings.LastOAuthTokenSecret = txtTokenSecret.Text;
            settings.Parameters = CreateParameterList();
            settings.WebAuthUsername = txtWebAuthUsername.Text;
            settings.RequestMethod = GetRequestMethod();

            return settings;
        }

        private List<ApiParameter> CreateParameterList()
        {
            var paramList = new List<ApiParameter>();
            foreach (var item in lstParameters.Items)
                if (item is ApiParameter)
                    paramList.Add((ApiParameter)item);
            return paramList;
        }

        private void SetEditorControls(ExtApiSettings settings)
        {
            settings = settings ?? new ExtApiSettings();

            txtAccessToken.Text = settings.LastOAuthAccessToken;
            txtApiUrl.Text = settings.LastApiUrl;
            txtConsumerKey.Text = settings.LastOAuthConsumerKey;
            txtConsumerSecret.Text = settings.LastOAuthConsumerSecret;
            txtTokenSecret.Text = settings.LastOAuthTokenSecret;
            chkIncludeOAuth.Checked = !string.IsNullOrWhiteSpace(settings.LastOAuthConsumerKey);
            chkUseWebAuth.Checked = !string.IsNullOrWhiteSpace(settings.WebAuthUsername);
            txtWebAuthUsername.Text = settings.WebAuthUsername;

            radGetRequest.Checked = (settings.RequestMethod == RequestMethod.Get);
            radPostRequest.Checked = (settings.RequestMethod == RequestMethod.Post);

            lstParameters.Items.Clear();
            if (settings.Parameters != null)
                foreach (var param in settings.Parameters)
                    lstParameters.Items.Add(param);
        }

        private RequestMethod GetRequestMethod()
        {
            RequestMethod selectedMethod;
            if (radGetRequest.Checked)
                selectedMethod = RequestMethod.Get;
            else if (radPostRequest.Checked)
                selectedMethod = RequestMethod.Post;
            else
                throw new InvalidOperationException("No request method selected");
            return selectedMethod;
        }
    }
}