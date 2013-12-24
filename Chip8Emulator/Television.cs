using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace Chip8Emulator
{
    public partial class Television : Form
    {
        private Chip8 chip8;
        private bool started;
        private Bitmap screen;

        public Television()
        {
            InitializeComponent();
            chip8 = new Chip8();
            screen = chip8.CreateBitmap();

            this.KeyPreview = true;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            DoubleBuffered = true;

            Thread t = new Thread(RunConsole);
            t.IsBackground = true;
            t.Start();


        }

        private void RunConsole()
        {
            while (true)
            {
                if (started)
                {
                    chip8.Step();

                    Invalidate();
                }
                Thread.Sleep(5);

            }
        }


        private static int? GetKeyIdx(int keyValue)
        {
            if (keyValue >= 48 && keyValue <= 57)
            {
                return keyValue - 48;
            }
            else if (keyValue >= 112 && keyValue <= 117)
            {
                return keyValue - 112 + 10;
            }
            else
            {
                return null;
            }
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var idx = GetKeyIdx(e.KeyValue);
            if (idx.HasValue)
            {
                chip8.SetKeyDown(idx.Value);
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            var idx = GetKeyIdx(e.KeyValue);
            if (idx.HasValue)
            {
                chip8.SetKeyUp(idx.Value);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            chip8.CopyToBitmap(screen);
            int scaleUp = 4;
            e.Graphics.DrawImage(screen, new Rectangle(0, 0, screen.Width * scaleUp, screen.Height * scaleUp), 0, 0, screen.Width,
                                 screen.Height, GraphicsUnit.Pixel);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FileDialog f= new OpenFileDialog();
            var result = f.ShowDialog();
            string file = null;
            if (result == DialogResult.OK)
            {
                file = f.FileName;
            }
            chip8.LoadRomFromPath(file);
            //timer.Start();
            started = true;
        }
    }
}
