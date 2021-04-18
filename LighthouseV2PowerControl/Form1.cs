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
            ChangeColumnWidth();
            lvStatus.SizeChanged += (d, s) => ChangeColumnWidth();
        }

        public void Log(object msg, LogType type = LogType.log)
        {
            ListViewItem item = new ListViewItem();
            item.Text = msg.ToString();
            item.ForeColor = (type == LogType.log) ? Color.Green : Color.DarkRed;
            lvStatus.Items.Add(item);
        }

        public void BtnActive(bool active)
        {
            btnStop.Enabled = btnStart.Enabled = active;
        }

        public IEnumerable<Button> GetButtons()
        {
            return Controls.OfType<Button>();
        }

        private void ChangeColumnWidth()
        {
            foreach (ColumnHeader lvStatusColumn in lvStatus.Columns)
            {
                lvStatusColumn.Width = lvStatus.Width;
            }
        }
    }
}
