using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using CSScriptLibrary;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

namespace OpenHardwareMonitor.GUI
{
    public partial class ScriptForm : Form
    {
        public IControl Control { get; set; }

        public string ScriptText
        {
            get { return richTextBox1.Text; }
            set { richTextBox1.Text = value; }
        }

        public CheckState ScriptEnabled
        {
            get { return enableCheckBox.CheckState; }
            set { enableCheckBox.CheckState = value; }
        }

        public ScriptForm()
        {
            InitializeComponent();
        }

        private void checkButton_Click(object sender, EventArgs e)
        {
            string err;
            if (!ScriptManager.Instance.TryCompileScript(richTextBox1.Text, out err))
            {
                MessageBox.Show(err, "Fail to compile!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
                CheckButton.Enabled = false;
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            CheckButton.Enabled = true;
        }
    }
}
