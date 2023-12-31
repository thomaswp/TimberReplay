using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TextBox = System.Windows.Forms.TextBox;
using Timer = System.Windows.Forms.Timer;

namespace TimberNet
{
    public partial class Form1 : Form
    {
        ClientDriver clientDriver;
        ServerDriver serverDriver;
        TimberClient client;
        TimberServer server;

        public Form1()
        {
            InitializeComponent();

            clientDriver = new ClientDriver();
            serverDriver = new ServerDriver();

            client = clientDriver.netBase;
            server = serverDriver.netBase;

            client.OnLog += message => Log(message, textBoxClient);
            server.OnLog += message => Log(message, textBoxServer);

            clientDriver.OnTargetSpeedChanged += speed => this.nudClientSpeed.Value = speed;
            serverDriver.OnTargetSpeedChanged += speed => this.nudServerSpeed.Value = speed;

            //server.Start();
        }

        private void Log(string message, TextBox textBox)
        {
            textBox.AppendText(message + "\r\n");
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void buttonStartServer_Click(object sender, EventArgs e)
        {
            if (server.Started) return;
            server.Start();
            buttonStartServer.Enabled = false;
        }

        private void buttonStartClient_Click(object sender, EventArgs e)
        {
            if (client.Started) return;
            client.Start();
            buttonStartClient.Enabled = false;
        }

        private void timerServer_Tick(object sender, EventArgs e)
        {
            serverDriver.TryTick();
        }

        private void timerClient_Tick(object sender, EventArgs e)
        {
            clientDriver.TryTick();
        }

        private void nudServerSpeed_ValueChanged(object sender, EventArgs e)
        {
            int speed = (int)nudServerSpeed.Value;
            UpdateSpeed(speed, timerServer);
        }

        private void nudClientSpeed_ValueChanged(object sender, EventArgs e)
        {
            int speed = (int)nudClientSpeed.Value;
            UpdateSpeed(speed, timerClient);
        }

        private void UpdateSpeed(int speed, Timer timer)
        {
            timer.Enabled = speed > 0;
            if (speed != 0)
            {
                timer.Interval = 300 / speed;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            clientDriver.Update();
            serverDriver.Update();
            labelClientTick.Text = "Tick: " + client.TickCount;
            labelServerTick.Text = "Tick: " + server.TickCount;
        }

        private void buttonStopServer_Click(object sender, EventArgs e)
        {
            server.Close();
        }

        private void buttonStopClient_Click(object sender, EventArgs e)
        {
            client.Close();
        }
    }
}