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

            richTextBox1.Text = @"
using System.Windows.Forms;

using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Utilities;

ScriptManager.ScriptOutput  calculateFanSpeed(IComputer computer)
{
    ScriptManager.ScriptOutput output;
    output.ControlMode = ControlMode.Software;
    output.FanSpeed = 100.0f;
    output.Reason = ""Default script!"";

    return output;
}";
        }

        private void checkButton_Click(object sender, EventArgs e)
        {
            testScript();
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            CheckButton.Enabled = true;
        }

        private void enableCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (enableCheckBox.CheckState == CheckState.Checked)
                testScript();
        }

        private void testScript()
        {
            string err;
            if (!ScriptManager.Instance.TryCompileScript(richTextBox1.Text, out err))
            {
                MessageBox.Show(err, "Fail to compile!", MessageBoxButtons.OK, MessageBoxIcon.Error);

                enableCheckBox.CheckState = CheckState.Unchecked;
            }
            else
                CheckButton.Enabled = false;
        }
    }
}
