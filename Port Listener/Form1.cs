using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;
using System.Net.NetworkInformation;

namespace TcpListenerApp
{
    public partial class Form1 : Form
    {
        private TcpListener listener;
        private bool running = false;
        private string outputOption = "Console";
        private string logFilePath = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cmbDataFormat.SelectedIndex = 0; // Set default to RAW

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("MMddyyyy_HHmmss");
            txtLogFilePath.Text = Path.Combine(currentDir, $"Port-Listener_{timestamp}.log");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!rbtnConsole.Checked && !rbtnLogFile.Checked && !rbtnBoth.Checked)
            {
                MessageBox.Show("Please select an output option.");
                return;
            }

            if (rbtnConsole.Checked)
                outputOption = "Console";
            else if (rbtnLogFile.Checked)
                outputOption = "LogFile";
            else if (rbtnBoth.Checked)
                outputOption = "Both";

            logFilePath = txtLogFilePath.Text;

            if (outputOption == "LogFile" || outputOption == "Both")
            {
                if (string.IsNullOrEmpty(logFilePath))
                {
                    MessageBox.Show("Please select a log file path.");
                    return;
                }
            }

            if (!int.TryParse(txtPort.Text, out int port))
            {
                MessageBox.Show("Please enter a valid port number.");
                return;
            }

            if (IsPortInUse(port))
            {
                MessageBox.Show($"Port {port} is already in use. Please select a different port.");
                return;
            }

            running = true;
            UpdateStatus("Running");
            Task.Run(() => StartListening(port));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            running = false;
            listener?.Stop();
            AppendText("Listener stopped.\n");
            UpdateStatus("Stopped");
        }

        private void StartListening(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            AppendText($"Listening on TCP port {port}...\n");

            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    AppendText("Client connected.\n");
                    Task.Run(() => HandleClient(client));
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    AppendText("\n");
                }
                catch (Exception ex)
                {
                    AppendText($"Error accepting client: {ex.Message}\n");
                }
            }

            listener.Stop();
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);

            try
            {
                string data = reader.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    string formattedData = FormatData(data);
                    AppendText($"Received:\n{formattedData}\n");

                    if (outputOption == "LogFile" || outputOption == "Both")
                    {
                        File.AppendAllText(logFilePath, $"Received:\n{formattedData}\n");
                    }
                }
                else
                {
                    AppendText("No data received.\n");
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error receiving data: {ex.Message}\n");
            }
            finally
            {
                reader.Close();
                client.Close();
            }
        }

        private string FormatData(string data)
        {
            StringBuilder formattedData = new StringBuilder();
            if (cmbDataFormat.SelectedItem.ToString() == "RAW")
            {
                formattedData.Append(data);
            }
            else if (cmbDataFormat.SelectedItem.ToString() == "RPOS Security Camera Journal")
            {
                string[] keyValues = data.Split(',');

                foreach (var kv in keyValues)
                {
                    var parts = kv.Split('=');
                    if (parts.Length == 2)
                    {
                        formattedData.AppendLine($"  {parts[0]}: {parts[1]}");
                    }
                }
            }

            return formattedData.ToString();
        }

        private void AppendText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendText), text);
            }
            else
            {
                txtOutput.AppendText(text + Environment.NewLine); // Ensure new lines are properly added
                txtOutput.SelectionStart = txtOutput.Text.Length; // Scroll to the bottom
                txtOutput.ScrollToCaret();
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Log files (*.txt)|*.txt|All files (*.*)|*.*";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    txtLogFilePath.Text = sfd.FileName;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
            listener?.Stop();
        }

        private bool IsPortInUse(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipGlobalProperties.GetActiveTcpListeners();

            return tcpConnections.Any(endpoint => endpoint.Port == port);
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), status);
            }
            else
            {
                toolStripStatusLabel.Text = status;
            }
        }
    }
}
