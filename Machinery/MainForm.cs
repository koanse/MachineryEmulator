using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using MathNet.Numerics.Distributions;
using System.IO;
using System.Collections;

namespace Machinery
{
    public partial class MainForm : Form
    {
        delegate void ReportDelegete(string s, MainForm form);
        static List<int>[] arrQueue;
        static int[][] arrUtil;
        static int[][] arrTr = { new int[] { 0, 1, 3, 5 }, new int[] { 1, 2, 3, 5 }, new int[] { 0, 2, 3, 4, 5 } }; // 5 - склад
        static Mutex mut = new Mutex();
        static AutoResetEvent evDis = new AutoResetEvent(false);
        static AutoResetEvent[][] evM;
        static Queue<object[]> qDis;
        public MainForm()
        {
            InitializeComponent();
            tb1.Text = tb2.Text = tb3.Text = "10";
        }
        static void DisThreadProc(object o)
        {
            MainForm form = o as MainForm;
            while (true)
            {
                evDis.WaitOne();
                lock (qDis)
                {
                    while (qDis.Count > 0)
                    {
                        object[] arrO = qDis.Dequeue();
                        int mType = (int)arrO[0], mIndex = (int)arrO[1];
                        int pType = arrUtil[mType][mIndex];
                        arrUtil[mType][mIndex] = -1;
                        for (int i = 0; i < arrTr[pType].Length; i++)
                        {
                            if (arrTr[pType][i] == mType)
                                arrQueue[arrTr[pType][i + 1]].Add(pType);
                        }
                    }
                }
                for (int i = 0; i < arrQueue.Length - 1; i++)
                {
                    if (arrQueue[i].Count == 0)
                        continue;
                    for (int j = 0; j < 3; j++)
                    {
                        while (arrQueue[i].Contains(j))
                        {
                            int k;
                            for (k = 0; k < arrUtil[i].Length; k++)
                            {
                                if (arrUtil[i][k] == -1)
                                {
                                    arrQueue[i].Remove(j);
                                    arrUtil[i][k] = j;
                                    evM[i][k].Set();
                                    break;
                                }
                            }
                            if (k == arrUtil[i].Length)
                                break;
                        }
                    }
                }
                string report = "Очереди:<br>";
                for (int i = 0; i < 5; i++)
                {
                    report += string.Format("Тип станка: {0} - (", i);
                    for (int j = 0; j < arrQueue[i].Count; j++)
                    {
                        report += string.Format(" {0} ", arrQueue[i][j]);
                    }
                    report += ")<br>";
                }
                report += "<br><br>Загрузка:<br>";
                for (int i = 0; i < 5; i++)
                {
                    report += string.Format("Тип станка: {0} - (", i);
                    for (int j = 0; j < arrUtil[i].Length; j++)
                    {
                        report += string.Format(" {0} ", arrUtil[i][j] == -1 ? "x" : arrUtil[i][j].ToString());
                    }
                    report += ")<br>";
                }
                report += string.Format("<br>Изготовлено: {0}", arrQueue[5].Count);
                form.Invoke(new ReportDelegete(Report), report, form);
            }
        }
        
        static void MThreadProc(object obj)
        {
            object[] arrO = obj as object[];
            int mType = (int)arrO[0], mIndex = (int)arrO[1], msec = (int)arrO[2];
            while (true)
            {
                evM[mType][mIndex].WaitOne();
                DateTime dtStart = DateTime.Now;
                double time = -1;
                if (mType == 0)
                {
                    NormalDistribution nd = new NormalDistribution(5, 2);
                    time = nd.NextDouble();
                }
                else if (mType == 1)
                    time = 3;
                else if (mType == 2)
                {
                    NormalDistribution nd = new NormalDistribution(6, 1);
                    time = nd.NextDouble();
                }
                else if (mType == 3)
                {
                    Random r = new Random();
                    time = 2 + 4 * r.NextDouble();
                }

                else if (mType == 4)
                {
                    NormalDistribution nd = new NormalDistribution(7, 2);
                    time = nd.NextDouble();
                }
                Thread.Sleep(Math.Abs((int)(time * msec)));
                mut.WaitOne();
                try
                {
                    StreamWriter sw = new StreamWriter("out.txt", true);
                    sw.WriteLine(string.Format("Станок: {0}-{1}, начало: {2}, конец: {3}",
                        mType, mIndex, dtStart, DateTime.Now));
                    sw.Close();
                }
                finally
                {
                    mut.ReleaseMutex();
                }
                lock (qDis)
                {
                    qDis.Enqueue(arrO);
                }
                evDis.Set();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                arrQueue = new List<int>[6];
                for (int i = 0; i < 6; i++)
                {
                    arrQueue[i] = new List<int>();
                }
                int[][] arr = { new int[] { -1, -1 }, new int[] { -1, -1, -1 }, new int[] { -1, -1 }, new int[] { -1, -1, -1 }, new int[] { -1 } };
                arrUtil = arr;
                evM = new AutoResetEvent[arr.Length][];
                for (int i = 0; i < arr.Length; i++)
                {
                    evM[i] = new AutoResetEvent[arr[i].Length];
                    for (int j = 0; j < arr[i].Length; j++)
                    {
                        evM[i][j] = new AutoResetEvent(false);
                    }
                }
                qDis = new Queue<object[]>();
                StreamWriter sw = new StreamWriter("out.txt", false);
                sw.Write("");
                sw.Close();
                int msec = (int)nud.Value;
                for (int i = 0; i < arrUtil.Length; i++)
                {
                    for (int j = 0; j < arrUtil[i].Length; j++)
                    {
                        Thread t = new Thread(new ParameterizedThreadStart(MThreadProc));
                        t.IsBackground = true;
                        t.Start(new object[] { i, j, msec });
                    }
                }
                Thread td = new Thread(new ParameterizedThreadStart(DisThreadProc));
                td.IsBackground = true;
                td.Start(this);
                for (int i = 0; i < int.Parse(tb1.Text); i++)
                {
                    arrQueue[0].Add(0);
                }
                for (int i = 0; i < int.Parse(tb2.Text); i++)
                {
                    arrQueue[1].Add(1);
                }
                for (int i = 0; i < int.Parse(tb3.Text); i++)
                {
                    arrQueue[0].Add(2);
                }
                button1.Enabled = false;
                nud.Enabled = false;
                evDis.Set();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        static void Report(string s, MainForm form)
        {
            form.wb.DocumentText = s;
        }
    }
}
