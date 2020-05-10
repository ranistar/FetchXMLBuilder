﻿using Cinteros.Xrm.FetchXmlBuilder.DockControls;
using Cinteros.Xrm.XmlEditorUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Cinteros.Xrm.FetchXmlBuilder.Controls
{
    public class FetchXmlElementControlBase : UserControl, IDefinitionSavable
    {
        private Dictionary<string, string> collec;
        private Dictionary<string, string> original;
        private string controlsCheckSum = "";
        private ErrorProvider errorProvider;
        private ErrorProvider warningProvider;
        private ErrorProvider infoProvider;
        private bool validationSuspended = false;

        static FetchXmlElementControlBase()
        {
            // Create the small warning icon to use for user feedback
            // https://stackoverflow.com/questions/3031124/is-there-a-way-to-get-different-sizes-of-the-windows-system-icons-in-net
            var iconSize = SystemInformation.SmallIconSize;
            var bitmapWarning = new Bitmap(iconSize.Width, iconSize.Height);
            using (var gw = Graphics.FromImage(bitmapWarning))
            {
                gw.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gw.DrawImage(SystemIcons.Warning.ToBitmap(), new Rectangle(Point.Empty, iconSize));
            }
            WarningIcon = Icon.FromHandle(bitmapWarning.GetHicon());
            var bitmapInfo = new Bitmap(iconSize.Width, iconSize.Height);
            using (var gi = Graphics.FromImage(bitmapInfo))
            {
                gi.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gi.DrawImage(SystemIcons.Information.ToBitmap(), new Rectangle(Point.Empty, iconSize));
            }
            InfoIcon = Icon.FromHandle(bitmapInfo.GetHicon());
        }

        protected static Icon WarningIcon { get; }
        protected static Icon InfoIcon { get; }

        public void InitializeFXB(Dictionary<string, string> collection, FetchXmlBuilder fetchXmlBuilder, TreeBuilderControl tree, TreeNode node)
        {
            fxb = fetchXmlBuilder;
            Node = node;
            Tree = tree;
            if (collection != null)
            {
                collec = collection;
            }
            else if (node != null)
            {
                collec = (Dictionary<string, string>)node.Tag;
            }

            original = new Dictionary<string, string>(collec);
            errorProvider = new ErrorProvider(this)
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink
            };
            warningProvider = new ErrorProvider(this)
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
                Icon = WarningIcon
            };
            infoProvider = new ErrorProvider(this)
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
                Icon = InfoIcon
            };
            PopulateControls();
            ControlUtils.FillControls(collec, this.Controls, this);
            controlsCheckSum = ControlUtils.ControlsChecksum(this.Controls);
            Saved += tree.CtrlSaved;
            AttachValidatingEvent(this);
            ValidateControlRecursive(this);
        }

        protected FetchXmlBuilder fxb { get; set; }

        protected TreeNode Node { get; set; }

        protected TreeBuilderControl Tree { get; set; }

        protected bool ValidationSuspended
        {
            get => validationSuspended;
            set
            {
                var revalidate = validationSuspended && !value;
                validationSuspended = value;
                if (revalidate)
                {
                    ValidateControlRecursive(this);
                }
            }
        }

        protected virtual void PopulateControls() { }

        protected override void OnValidating(CancelEventArgs e)
        {
            base.OnValidating(e);

            if (RequiresSave())
            {
                e.Cancel = !Save(false);
            }
        }

        protected virtual bool RequiresSave()
        {
            return !controlsCheckSum.Equals(ControlUtils.ControlsChecksum(this.Controls));
        }

        public virtual bool Save(bool silent)
        {
            try
            {
                if (!ValidateControlRecursive(this))
                {
                    return false;
                }

                SaveInternal(silent);
            }
            catch (ArgumentNullException ex)
            {
                if (!silent)
                {
                    MessageBox.Show(ex.Message, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }

                return false;
            }

            controlsCheckSum = ControlUtils.ControlsChecksum(this.Controls);
            return true;
        }

        protected virtual void SaveInternal(bool silent)
        {
            SendSaveMessage(ControlUtils.GetAttributesCollection(this.Controls, true));
        }

        protected virtual ControlValidationResult ValidateControl(Control control)
        {
            return null;
        }

        private bool ValidateControlRecursive(Control control)
        {
            var result = ValidateControlAndShowIcons(control);
            var valid = result?.Level != ControlValidationLevel.Error;
            control.Controls.OfType<Control>().ToList().ForEach(c => valid &= ValidateControlRecursive(c));
            return valid;
        }

        private ControlValidationResult ValidateControlAndShowIcons(Control control)
        {
            if (validationSuspended)
            {
                return null;
            }
            var result = ValidateControl(control);
            errorProvider.SetError(control, result?.Level == ControlValidationLevel.Error ? result.Message : null);
            warningProvider.SetError(control, result?.Level == ControlValidationLevel.Warning ? result.Message : null);
            infoProvider.SetError(control, result?.Level == ControlValidationLevel.Info ? result.Message : null);
            return result;
        }

        private void AttachValidatingEvent(Control control)
        {
            control.Controls.OfType<Control>().ToList().ForEach(c =>
            {
                c.Validating += Control_Validating;
                AttachValidatingEvent(c);
            });
        }

        private void Control_Validating(object sender, CancelEventArgs e)
        {
            if (sender is Control ctrl)
            {
                ValidateControlAndShowIcons(ctrl);
            }
        }

        /// <summary>
        /// Sends a connection success message
        /// </summary>
        /// <param name="service">IOrganizationService generated</param>
        /// <param name="parameters">Lsit of parameter</param>
        private void SendSaveMessage(Dictionary<string, string> collection)
        {
            Saved?.Invoke(this, new SaveEventArgs { AttributeCollection = collection });
        }

        public event EventHandler<SaveEventArgs> Saved;

        protected void FillControl(Control control)
        {
            ControlUtils.FillControl(collec, control, this);
        }

        protected override bool ProcessKeyPreview(ref Message m)
        {
            const int WM_KEYDOWN = 0x0100;
            const int VK_ESCAPE = 27;

            if (m.Msg == WM_KEYDOWN && (int)m.WParam == VK_ESCAPE)
            {
                collec = new Dictionary<string, string>(original);
                ControlUtils.FillControls(collec, this.Controls, this);
                controlsCheckSum = ControlUtils.ControlsChecksum(this.Controls);
                SendSaveMessage(original);
                return true;
            }

            return base.ProcessKeyPreview(ref m);
        }
    }

    public enum ControlValidationLevel
    {
        Success,
        Info,
        Warning,
        Error
    }

    public class ControlValidationResult
    {
        public ControlValidationResult(ControlValidationLevel level, string message)
        {
            Level = level;
            Message = message;
        }
        public ControlValidationLevel Level { get; }
        public string Message { get; }
    }
}