using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;


namespace LighthouseV2PowerControl
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public void Log(object msg, LogType type = LogType.log)
        {
            if (type == LogType.error)
            {
                lvStatus.Items.Add(msg.ToString());
                lvStatus.Items[lvStatus.Items.Count - 1].ForeColor = Color.DarkRed;
            }
            else if(type == LogType.log)
            {
                lvStatus.Items.Add(msg.ToString());
                lvStatus.Items[lvStatus.Items.Count - 1].ForeColor = Color.Green;
            }
        }

        public void BtnActive(bool active)
        {
            btnStop.Enabled = btnStart.Enabled = active;
        }

        public IEnumerable<Button> GetButtons()
        {
            return Controls.OfType<Button>();
        }
    }
}
