using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;


namespace LighthouseV2PowerControl
{
    sealed partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            ChangeColumnWidth();
            Debug.Assert(true);
            lvStatus.SizeChanged += (d, s) => ChangeColumnWidth();
            Program.powerControl.StartAsync().ContinueWith(AfterStart);
        }

        public void Log(TaskResultAndMessage log)
        {
            ListViewItem item = new ListViewItem();
            item.Text = log.message;
            item.ForeColor = (log.result == TaskResult.success) ? Color.Green : Color.DarkRed;
            if (lvStatus.InvokeRequired)
            {
                lvStatus.Invoke(new Action<ListViewItem>((i) => lvStatus.Items.Add(i)), item);
            }
            else
            {
                lvStatus.Items.Add(item);
            }
        }

        public void Log(ListViewItem log)
        {
            if (lvStatus.InvokeRequired)
            {
                lvStatus.Invoke(new Action<ListViewItem>((i) => lvStatus.Items.Add(i)), log);
            }
            else
            {
                lvStatus.Items.Add(log);
            }
        }

        private void ChangeColumnWidth()
        {
            foreach (ColumnHeader lvStatusColumn in lvStatus.Columns)
            {
                lvStatusColumn.Width = lvStatus.Width;
            }
        }

        #region Buttons
        private void btnStart_Click(object sender, EventArgs e)
        {
            StartOrStop(true);
        }


        private void btnStop_Click(object sender, EventArgs e)
        {
            StartOrStop(false);
        }

        private void StartOrStop(bool status)
        {
            Program.powerControl.SendOnAllLighthouseAsync(status).ContinueWith((a) => AfterLighthouseStop(a, status));
            BtnActive(false);
        }

        private void btnReg_Click(object sender, EventArgs e)
        {

        }

        private void btnRm_Click(object sender, EventArgs e)
        {

        }

        public IEnumerable<Button> GetButtons()
        {
            return Controls.OfType<Button>();
        }

        public void BtnActive(bool active)
        {
            if (btnStart.InvokeRequired)
            {
                btnStart.Invoke(new Action<bool>((i) => btnStart.Enabled = i), active);
            }
            else
            {
                btnStart.Enabled = active;
            }

            if (btnStop.InvokeRequired)
            {
                btnStop.Invoke(new Action<bool>((i) => btnStop.Enabled = i), active);
            }
            else
            {
                btnStop.Enabled = active;
            }
        }

        #endregion

        #region ContinueWith
        private void AfterStart(Task<List<TaskResultAndMessage>> arg)
        {
            bool status = true;
            foreach (var task in arg.Result)
            {
                Log(task);
                if (task.result == TaskResult.failure)
                {
                    status = false;
                }
            }
            BtnActive(status);
        }

        private void AfterLighthouseStop(Task<List<TaskResultAndMessage>> arg, bool status)
        {
            foreach (var task in arg.Result)
            {
                var temp = task;
                if (temp.message == string.Empty)
                {
                    temp.message = "Lighthouse";
                    temp.message += status ? " start;" : " stop;";
                }
                Log(temp);
            }
            BtnActive(true);
        }

        #endregion
    }

    public static class ListExtention
    {

    }
}
