﻿using Cinteros.Xrm.XmlEditorUtils;
using System;
using System.Web;
using System.Windows.Forms;
using System.Xml;

namespace Cinteros.Xrm.FetchXmlBuilder.DockControls
{
    public partial class XmlContentDisplayDialog : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        public XmlNode result;
        public bool execute;
        private string findtext = "";
        FetchXmlBuilder fxb;
        SaveFormat format;

        internal static XmlContentDisplayDialog ShowDialog(string xmlString, string header, SaveFormat saveFormat, FetchXmlBuilder caller)
        {
            if (xmlString.Length > 100000)
            {
                var dlgresult = MessageBox.Show("Huge result, this may take a while!\n" + xmlString.Length.ToString() + " characters in the XML document.\n\nContinue?", "Huge result",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dlgresult == DialogResult.No)
                {
                    return null;
                }
            }
            var xcdDialog = new XmlContentDisplayDialog(header, false, saveFormat, caller);
            xcdDialog.UpdateXML(xmlString);
            xcdDialog.StartPosition = FormStartPosition.CenterParent;
            xcdDialog.ShowDialog();
            return xcdDialog;
        }

        internal XmlContentDisplayDialog(FetchXmlBuilder caller) : this("FetchXML", true, SaveFormat.XML, caller) { }

        private XmlContentDisplayDialog(string header, bool allowEdit, SaveFormat saveFormat, FetchXmlBuilder caller)
        {
            InitializeComponent();
            format = saveFormat;
            fxb = caller;
            result = null;
            execute = false;
            Text = string.IsNullOrEmpty(header) ? "FetchXML Builder" : header;
            TabText = Text;
            txtXML.KeyUp += fxb.LiveXML_KeyUp;
            panLiveUpdate.Visible = allowEdit;
            panCancel.Visible = !allowEdit;
            panOk.Visible = allowEdit;
            panFormatting.Visible = allowEdit;
            panExecute.Visible = allowEdit;
            panSave.Visible = format != SaveFormat.None;
            chkLiveUpdate.Checked = allowEdit && fxb.currentSettings.xmlLiveUpdate;
            UpdateButtons();
        }

        private void btnFormat_Click(object sender, EventArgs e)
        {
            FormatXML(false);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            fxb.dockControlBuilder.Init(txtXML.Text, "manual edit", true);
        }

        private void FormatXML(bool silent)
        {
            try
            {
                txtXML.Process();
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    MessageBox.Show(ex.Message, "XML Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void XmlContentDisplayDialog_KeyDown(object sender, KeyEventArgs e)
        {
            RichTextBox textBox = txtXML;
            findtext = FindTextHandler.HandleFindKeyPress(e, textBox, findtext);
        }

        public void UpdateXML(string xmlString)
        {
            txtXML.Text = xmlString;
            txtXML.Settings.QuoteCharacter = fxb.currentSettings.useSingleQuotation ? '\'' : '"';
            FormatXML(true);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            fxb.FetchResults(txtXML.Text);
        }

        private void XmlContentDisplayDialog_Load(object sender, EventArgs e)
        {
            if (DialogResult == DialogResult.Cancel)
            {
                Close();
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = $"Save {format}",
                Filter = $"{format} file (*.{format.ToString().ToLowerInvariant()})|*.{format.ToString().ToLowerInvariant()}"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                txtXML.SaveFile(sfd.FileName, RichTextBoxStreamType.PlainText);
                MessageBox.Show($"{format} saved to {sfd.FileName}");
            }
        }

        private void FormatAsXML()
        {
            if (FetchIsHtml())
            {
                txtXML.Text = HttpUtility.HtmlDecode(txtXML.Text.Trim());
            }
            else if (FetchIsEscaped())
            {
                txtXML.Text = Uri.UnescapeDataString(txtXML.Text.Trim());
            }
            else
            {
                if (MessageBox.Show("Unrecognized encoding, unsure what to do with it.\n" +
                    "Currently FXB can handle htmlencoded and urlescaped strings.\n\n" +
                    "Would you like to submit an issue to FetchXML Builder to be able to handle this?",
                    "Decode FetchXML", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("https://github.com/Innofactor/FetchXMLBuilder/issues/new");
                }
                return;
            }
            FormatXML(false);
        }

        private void FormatAsHtml()
        {
            var response = MessageBox.Show("Strip spaces from encoded XML?", "Encode XML", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (response == DialogResult.Cancel)
            {
                UpdateButtons();
                return;
            }
            if (!FetchIsPlain())
            {
                FormatAsXML();
            }
            var xml = response == DialogResult.Yes ? GetCompactXml() : txtXML.Text;
            txtXML.Text = HttpUtility.HtmlEncode(xml);
        }

        private void FormatAsEsc()
        {
            if (!FetchIsPlain())
            {
                FormatAsXML();
            }
            txtXML.Text = Uri.EscapeDataString(GetCompactXml());
        }

        private string GetCompactXml()
        {
            if (!FetchIsPlain())
            {
                FormatAsXML();
            }
            var xml = txtXML.Text;
            while (xml.Contains(" <")) xml = xml.Replace(" <", "<");
            while (xml.Contains(" >")) xml = xml.Replace(" >", ">");
            while (xml.Contains(" />")) xml = xml.Replace(" />", "/>");
            return xml.Trim();
        }

        private bool FetchIsPlain()
        {
            return txtXML.Text.Trim().ToLowerInvariant().StartsWith("<fetch");
        }

        private bool FetchIsHtml()
        {
            return txtXML.Text.Trim().ToLowerInvariant().StartsWith("&lt;fetch");
        }

        private bool FetchIsEscaped()
        {
            return txtXML.Text.Trim().ToLowerInvariant().StartsWith("%3cfetch");
        }

        private void txtXML_TextChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var plain = FetchIsPlain();
            rbFormatEsc.Checked = FetchIsEscaped();
            rbFormatHTML.Checked = FetchIsHtml();
            rbFormatXML.Checked = plain;
            btnFormat.Enabled = plain;
            btnExecute.Enabled = plain && !fxb.currentSettings.xmlLiveUpdate;
            btnOk.Enabled = !fxb.currentSettings.xmlLiveUpdate;
        }

        private void XmlContentDisplayDialog_DockStateChanged(object sender, EventArgs e)
        {
            if (DockState == WeifenLuo.WinFormsUI.Docking.DockState.Unknown)
            {
                if (this == fxb.dockControlXml)
                {
                    fxb.dockControlXml = null;
                }
            }
            if (DockState != WeifenLuo.WinFormsUI.Docking.DockState.Unknown &&
                DockState != WeifenLuo.WinFormsUI.Docking.DockState.Hidden)
            {
                fxb.currentSettings.xmlDockState = DockState;
            }
        }

        private void rbFormatXML_Click(object sender, EventArgs e)
        {
            FormatAsXML();
        }

        private void rbFormatHTML_Click(object sender, EventArgs e)
        {
            FormatAsHtml();
        }

        private void rbFormatEsc_Click(object sender, EventArgs e)
        {
            FormatAsEsc();
        }

        private void chkLiveUpdate_CheckedChanged(object sender, EventArgs e)
        {
            if (panLiveUpdate.Visible)
            {
                fxb.currentSettings.xmlLiveUpdate = chkLiveUpdate.Checked;
            }
            UpdateButtons();
        }
    }

    internal enum SaveFormat
    {
        None = 0,
        XML = 1,
        JSON = 2
    }
}
