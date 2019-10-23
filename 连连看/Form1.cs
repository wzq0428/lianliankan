using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Media;
using Microsoft.DirectX.DirectSound;

namespace 连连看
{

    public partial class Form1 : Form
    {
        private int[] number;//每行(每列)图片的数量
        private int[] imageNumber;//不同关卡使用的图片数量
        private string[] sdifficulty;//存储难度(简单、一般、困难)的字符串
        private Button[,] buttons;//按钮
        private Image[] images;//图片文件
        private int difficulty;//难度的下标，0：简单，1：一般，2：困难
        public int Difficulty
        {
            get
            {
                return difficulty;
            }
            set
            {
                if (value != difficulty)
                {
                    difficulty = value;
                    this.CurDifficluty.Text = "难度：" + sdifficulty[difficulty];
                    RemoveButton();
                    GenerateButton();
                    DisableButton();
                }
            }
        }
        private int level;//关卡等级:0,1,2,3,4
        public int Level
        {
            get
            {
                return level;
            }
            set
            {
                if (value != level)
                {
                    level = value;
                    this.pBar.Maximum = 180 - 20 * level;
                    this.CurLevel.Text = "当前关卡：" + (level + 1);
                    RemoveButton();
                    GenerateButton();
                    DisableButton();
                }
            }
        }
        private int time;//剩余时间
        public int Time
        {
            get
            {
                return time;
            }
            set
            {
                time = value;
                this.RestTime.Text = "剩余时间：" + time + "s";
                this.pBar.Value = time;
            }
        }
        private int score;//当前得分
        public int Score
        {
            get
            {
                return score;
            }
            set
            {
                if (value != score)
                    initialed = false;
                score = value;
                this.CurScore.Text = "当前得分：" + score;
                if (score > MaxScore)
                {
                    MaxScore = score;
                }
            }
        }
        private int maxScore;//最高分
        public int MaxScore
        {
            get
            {
                return maxScore;
            }
            set
            {
                maxScore = value;
                this.HighestScore.Text = "最高分：" + maxScore;
            }
        }
        private int totalNumber;//总的按钮个数
        private int removedNumber;//已经消除的按钮个数
        private bool initialed;//界面是否初始化，Score发生改变或者End按钮事件触发时，initialed都置为false，下一次开始时需要重新加载，GenerateButton函数会把initialed置为true。
        private Button choosedButton;//第一次选中的按钮
        private int[,] imageIndex;//存储每个按钮对应的图片的下标
        private bool[,] visible;//存储每个按钮是否可见(未消除)，但是还要在buttons周围加一圈，方便计算是否可达
        private SoundPlayer backGround;//背景音乐


        public Form1()
        {
            InitializeComponent();
            this.Width = 900;
            this.Height = 800;
            number = new int[3] { 10, 12, 14 };
            imageNumber = new int[5] { 9, 10, 11, 12, 13 };
            sdifficulty = new string[3] { "简单", "一般", "困难" };
            Difficulty = 0;
            Level = 0;
            Time = 180 - Level * 20;
            this.pBar.Maximum = 180 - Level * 20;
            DirectoryInfo dir = new DirectoryInfo(string.Format(Application.StartupPath));
            string[] path = Directory.GetFiles(dir.Parent.Parent.GetDirectories("images")[0].FullName, "*.png");
            int len = path.Length;
            this.images = new Image[len];
            for (int i = 0; i < len; ++i)
            {
                images[i] = Image.FromFile(path[i]);
            }
            timer1.Enabled = false;
            this.RestTime.SetBounds(50, 30, 100, 50);
            this.pBar.SetBounds(200, 30, 600, 25);
            this.HighestScore.SetBounds(50, 130, 100, 25);
            this.CurScore.SetBounds(50, 230, 100, 25);
            this.CurDifficluty.SetBounds(50, 330, 100, 25);
            this.CurLevel.SetBounds(50, 430, 100, 25);
            this.MusicOnOff.SetBounds(50, 530, 100, 25);
            this.Start.SetBounds(150, 700, 100, 25);
            this.Pause.SetBounds(300, 700, 100, 25);
            this.Continue.SetBounds(450, 700, 100, 25);
            this.End.SetBounds(600, 700, 100, 25);
            backGround = new SoundPlayer(global::连连看.Properties.Resources.背景音效);
            if (MusicOnOff.Checked)
                backGround.PlayLooping();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            GenerateButton();
            DisableButton();
            if (File.Exists("maxScore.txt"))
            {
                using (StreamReader sr = new StreamReader("maxScore.txt"))
                {
                    if (int.TryParse(sr.ReadLine(), out int maxScore))
                        MaxScore = maxScore;
                    else
                        MaxScore = 0;
                }
            }
            else
            {
                MaxScore = 0;
            }
        }

        private void Start_Click(object sender, EventArgs e)
        {
            if (!initialed)
            {
                RemoveButton();
                GenerateButton();
            }
            totalNumber = number[Difficulty] * number[Difficulty];
            removedNumber = 0;
            Time = 180 - Level * 20;
            Score = 0;
            EnableButton();
            timer1.Enabled = true;
        }

        private void Pause_Click(object sender, EventArgs e)
        {
            DisableButton();
            timer1.Enabled = false;
        }

        private void Continue_Click(object sender, EventArgs e)
        {
            EnableButton();
            timer1.Enabled = true;
        }

        private void End_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            MessageBox.Show("您的最终得分为" + Score + "!", "游戏结束");
            initialed = false;
            Level = 0;
            Score = 0;
            Time = 180 - 20 * Level;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (Time > 0 && removedNumber < totalNumber)
            {
                --Time;
            }
            else if (removedNumber == totalNumber && Level < 4)
            {
                if (MusicOnOff.Checked)
                {
                    SecondaryBuffer secBuffer;//缓冲区对象    
                    Device secDev;//设备对象    
                    secDev = new Device();
                    secDev.SetCooperativeLevel(this, CooperativeLevel.Normal);//设置设备协作级别    
                    secBuffer = new SecondaryBuffer(global::连连看.Properties.Resources.通关音效, secDev);//创建辅助缓冲区    
                    secBuffer.Play(0, BufferPlayFlags.Default);//设置缓冲区为默认播放   
                }
                Score += (Difficulty + Level + 1) * Time;//奖励剩余时间对应的分数
                timer1.Enabled = false;
                DialogResult dr = MessageBox.Show("恭喜您通过本关，点击确定进入下一关，点击取消将退出本局游戏", "通关啦！分数加成" + (Difficulty + Level + 1) * Time, MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK)
                {
                    ++Level;
                    EnableButton();
                    totalNumber = number[Difficulty] * number[Difficulty];
                    removedNumber = 0;
                    Time = 180 - Level * 20;
                    timer1.Enabled = true;
                }
                else if (dr == DialogResult.Cancel)
                {
                    End.PerformClick();
                }
            }
            else
            {
                End.PerformClick();
            }
        }

        private void 简单ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Difficulty = 0;
        }

        private void 一般ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Difficulty = 1;
        }

        private void 困难ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Difficulty = 2;
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            Level = 0;
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            Level = 1;
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            Level = 2;
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            Level = 3;
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            Level = 4;
        }

        private bool IsArrivable0(int x1, int y1, int x2, int y2)//直线可达
        {
            int n = number[Difficulty];
            if (x1 == x2)
            {
                if (y1 < y2)
                {
                    for (int y = y1 + 1; y < y2; ++y)
                    {
                        if (visible[x1, y])
                            return false;
                    }
                    return true;
                }
                else
                {
                    for (int y = y1 - 1; y > y2; --y)
                    {
                        if (visible[x1, y])
                            return false;
                    }
                    return true;
                }
            }
            else if (y1 == y2)
            {
                if (x1 < x2)
                {
                    for (int x = x1 + 1; x < x2; ++x)
                    {
                        if (visible[x, y1])
                            return false;
                    }
                    return true;
                }
                else
                {
                    for (int x = x1 - 1; x > x2; --x)
                    {
                        if (visible[x, y1])
                            return false;
                    }
                    return true;
                }
            }
            else
                return false;
        }

        private bool IsArrivable1(int x1, int y1, int x2, int y2)//拐一个角可达
        {
            int n = number[Difficulty];
            for (int i = 0; i < n + 2; ++i)
            {
                if (i == x1)
                    continue;
                if (!visible[i, y1] && IsArrivable0(x1, y1, i, y1) && IsArrivable0(x2, y2, i, y1))
                    return true;
            }
            for (int i = 0; i < n + 2; ++i)
            {
                if (i == y1)
                    continue;
                if (!visible[x1, i] && IsArrivable0(x1, y1, x1, i) && IsArrivable0(x2, y2, x1, i))
                    return true;
            }
            return false;
        }

        private bool IsArrivable2(int x1, int y1, int x2, int y2)//拐两个角可达
        {
            int n = number[Difficulty];
            for (int i = 0; i < n + 2; ++i)
            {
                if (i == x1)
                    continue;
                if (!visible[i, y1] && IsArrivable0(x1, y1, i, y1) && IsArrivable1(x2, y2, i, y1))
                    return true;
            }
            for (int i = 0; i < n + 2; ++i)
            {
                if (i == y1)
                    continue;
                if (!visible[x1, i] && IsArrivable0(x1, y1, x1, i) && IsArrivable1(x2, y2, x1, i))
                    return true;
            }
            return false;
        }

        private bool IsArrivable(int x1, int y1, int x2, int y2)
        {
            int n = number[Difficulty];
            return IsArrivable0(x1 + 1, y1 + 1, x2 + 1, y2 + 1)
                || IsArrivable1(x1 + 1, y1 + 1, x2 + 1, y2 + 1)
                || IsArrivable2(x1 + 1, y1 + 1, x2 + 1, y2 + 1);
        }

        private void DisableButton()
        {
            foreach (Button btn in buttons)
            {
                btn.Enabled = false;
            }
        }

        private void EnableButton()
        {
            foreach (Button btn in buttons)
            {
                btn.Enabled = true;
            }
        }

        private void GenerateButton()
        {
            int n = number[Difficulty];
            int m = imageNumber[Level];
            buttons = new Button[n, n];
            imageIndex = new int[n, n];
            visible = new bool[n + 2, n + 2];
            int startX = 200, startY = 75;
            int delta = 600 / n;
            int w = delta - 5;
            for (int i = 0; i < n; ++i)
            {
                for (int j = 0; j < n; ++j)
                {
                    Button btn = new Button();
                    btn.SetBounds(startX + delta * i, startY + delta * j, w, w);
                    btn.Visible = false;
                    buttons[i, j] = btn;
                    visible[i + 1, j + 1] = true;
                    btn.Tag = i * n + j;
                    btn.Click += ButtonClick;
                    this.Controls.Add(btn);
                }
            }
            int totalNum = n * n;
            Random rnd = new Random();
            while (totalNum > 0)
            {
                int x = rnd.Next(n), y = rnd.Next(n), i = rnd.Next(m);
                Button btn = buttons[x, y];
                while (btn.Visible)
                {
                    x = rnd.Next(n);
                    y = rnd.Next(n);
                    btn = buttons[x, y];
                }
                btn.BackgroundImage = images[i];
                btn.BackgroundImageLayout = ImageLayout.Zoom;
                btn.UseVisualStyleBackColor = true;
                btn.Visible = true;
                imageIndex[x, y] = i;
                while (btn.Visible)
                {
                    x = rnd.Next(n);
                    y = rnd.Next(n);
                    btn = buttons[x, y];
                }
                btn.BackgroundImage = images[i];
                btn.BackgroundImageLayout = ImageLayout.Zoom;
                btn.UseVisualStyleBackColor = true;
                btn.Visible = true;
                imageIndex[x, y] = i;
                totalNum -= 2;
            }
            initialed = true;
            choosedButton = null;
        }

        private void RemoveButton()
        {
            foreach (Button btn in buttons)
            {
                this.Controls.Remove(btn);
            }
        }

        private void ButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (choosedButton == null)
            {
                choosedButton = btn;
                btn.Focus();
            }
            else
            {
                int n = number[Difficulty];
                int tag1 = int.Parse(choosedButton.Tag.ToString());
                int tag2 = int.Parse(btn.Tag.ToString());
                int x1 = tag1 / n, y1 = tag1 % n;
                int x2 = tag2 / n, y2 = tag2 % n;
                if (!(x1 == x2 && y1 == y2) && imageIndex[x1, y1] == imageIndex[x2, y2] && IsArrivable(x1, y1, x2, y2))
                {
                    if (MusicOnOff.Checked)
                    {
                        SecondaryBuffer secBuffer;//缓冲区对象    
                        Device secDev;//设备对象    
                        secDev = new Device();
                        secDev.SetCooperativeLevel(this, CooperativeLevel.Normal);//设置设备协作级别    
                        secBuffer = new SecondaryBuffer(global::连连看.Properties.Resources.消除音效, secDev);//创建辅助缓冲区    
                        secBuffer.Play(0, BufferPlayFlags.Default);//设置缓冲区为默认播放   
                    }
                    choosedButton.Visible = false;
                    btn.Visible = false;
                    visible[x1 + 1, y1 + 1] = false;
                    visible[x2 + 1, y2 + 1] = false;
                    removedNumber += 2;
                    Score += (Difficulty + Level + 1) * 2;
                    choosedButton = null;
                }
                else
                {
                    if (MusicOnOff.Checked)
                    {
                        SecondaryBuffer secBuffer;//缓冲区对象    
                        Device secDev;//设备对象    
                        secDev = new Device();
                        secDev.SetCooperativeLevel(this, CooperativeLevel.Normal);//设置设备协作级别    
                        secBuffer = new SecondaryBuffer(global::连连看.Properties.Resources.消除失败音效, secDev);//创建辅助缓冲区    
                        secBuffer.Play(0, BufferPlayFlags.Default);//设置缓冲区为默认播放   
                    }
                    choosedButton = null;
                }
            }
        }

        private void MusicOnOff_CheckedChanged(object sender, EventArgs e)
        {
            if(MusicOnOff.Checked)
            {
                backGround.PlayLooping();
            }
            else
            {
                backGround.Stop();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!File.Exists("maxScore.txt"))
                File.Create("maxScore.txt");
            using (StreamWriter sw = new StreamWriter("maxScore.txt"))
            {
                sw.WriteLine(MaxScore);
            }
        }
    }

    public class NewProgressBar : ProgressBar
    {
        private Brush[] brushes = new Brush[] { Brushes.Red, Brushes.Yellow, Brushes.Orange, Brushes.Lime, };
        public NewProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rec = e.ClipRectangle;

            double rate = (double)Value / Maximum;
            rec.Width = (int)(rec.Width * rate) - 4;
            if (ProgressBarRenderer.IsSupported)
                ProgressBarRenderer.DrawHorizontalBar(e.Graphics, e.ClipRectangle);
            rec.Height = rec.Height - 4;
            if (rate >= 0.75)
                e.Graphics.FillRectangle(Brushes.Lime, 2, 2, rec.Width, rec.Height);
            else if (rate >= 0.5)
                e.Graphics.FillRectangle(Brushes.Yellow, 2, 2, rec.Width, rec.Height);
            else if (rate >= 0.25)
                e.Graphics.FillRectangle(Brushes.Orange, 2, 2, rec.Width, rec.Height);
            else
                e.Graphics.FillRectangle(Brushes.Red, 2, 2, rec.Width, rec.Height);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);

        }
    }

    //public static class ModifyProgressBarColor
    //{
    //    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    //    static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);
    //    public static void SetState(this ProgressBar pBar, int state)
    //    {
    //        SendMessage(pBar.Handle, 1040, (IntPtr)state, IntPtr.Zero);
    //    }
    //}

}
