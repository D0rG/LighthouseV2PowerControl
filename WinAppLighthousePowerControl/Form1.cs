﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace WinAppLighthousePowerControl
{
    sealed partial class Form1 : Form
    {
        private bool initWithSteamVR = false;
        public Form1()
        {
            InitializeComponent();
            ChangeColumnWidth();
            btnStart.Click += ((e, a) => StartOrStop(true));
            btnStop.Click += ((e, a) => StartOrStop(false));
            btnReg.Click += ((e, a) => Manifest(ManifestTask.add));
            btnRm.Click += ((e, a) => Manifest(ManifestTask.rm));
            lvStatus.SizeChanged += (d, s) => ChangeColumnWidth();

            var steamVRres = Program.powerControl.ConnectToSteamVR();
            if (steamVRres.result == TaskResult.success)
            {
                initWithSteamVR = true;
            }
            Log(steamVRres);
            Program.powerControl.UpdateLighthouseListAsync().ContinueWith(AfterUpdateLighthouseList);
        }

        public Form1(string s)
        {

        }

        #region Log
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

        #endregion

        #region Buttons

        private void StartOrStop(bool status)
        {
            if (status)
            {
                Program.powerControl.ActivateAllLighthouseAsync().ContinueWith((a) => AfterLighthouseChangeState(a, status));
            }
            else
            {
                Program.powerControl.DeactivateAllLighthouseAsync().ContinueWith((a) => AfterLighthouseChangeState(a, status));
            }
            BtnActive(false);
        }

        private void Manifest(ManifestTask task)
        {
            var res = Program.powerControl.AppManifest(task);
            Log(res);
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
        private void AfterUpdateLighthouseList(Task<List<TaskResultAndMessage>> arg)
        {
            if (arg.Result.Count == 0)
            {
                BtnActive(true);
                if (initWithSteamVR)
                {
                    Program.powerControl.ActivateAllLighthouseAsync().ContinueWith(task => AfterLighthouseChangeState(task, true));
                }
            }
            else
            {
                foreach (var task in arg.Result)
                {
                    Log(task);
                }
            }
        }

        private void AfterLighthouseChangeState(Task<List<TaskResultAndMessage>> arg, bool status)
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
}
