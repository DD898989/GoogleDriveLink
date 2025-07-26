using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;// also ref by Google.Apis.Core

namespace GoogleDriveLink
{
    public partial class ClipboardForm : Form
    {
        public ClipboardForm()
        {
            this.Load += ClipboardForm_Load;
            InitializeComponent();
        }

        private void ClipboardForm_Load(object sender, EventArgs e)
        {
            ClipboardNotification.ClipboardUpdate += OnClipboardChanged;
            OnClipboardChanged(sender, e);
        }

        private void OnClipboardChanged(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                string copiedText = Clipboard.GetText();
                textLabel.Text = "Text:\n" + copiedText;
                pictureBox.Image = null;
            }
            else if (Clipboard.ContainsImage())
            {
                Image image = Clipboard.GetImage();
                textLabel.Text = "Image，size: " + image.Size;
                pictureBox.Image = image;
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                string display = $"File:\n{files[0]}";

                textLabel.Text = display;
                pictureBox.Image = null;
            }
        }
        public const string KeyFilePath = "ServiceAccountKey.json";
        private void button1_Click(object sender, EventArgs e)
        {

            var service = AuthenticateServiceAccount(
                KeyFilePath,
                new[] { DriveService.Scope.DriveFile, DriveService.Scope.Drive }
                );

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Parents = new List<string> { File.ReadAllText("ParentFolderID.txt") }
            };

            var memoryStream = new MemoryStream();

            try
            {
                fileMetadata.Name = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "_" + ".png";
                Image image = Clipboard.GetImage();
                image.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch
            {
                var f = Clipboard.GetFileDropList()[0];
                var winFile = new FileInfo(f);
                fileMetadata.Name = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "_" + winFile.Name;

                var b = File.ReadAllBytes(f);
                memoryStream = new MemoryStream(b);

            }

            var mimeType = "application/octet-stream";
            if (fileMetadata.Name.EndsWith("mp4"))
            {
                mimeType = "video/mp4";
            }
            if (fileMetadata.Name.EndsWith("mpeg"))
            {
                mimeType = "video/mpeg";
            }

            var request = service.Files.Create(fileMetadata, memoryStream, mimeType);
            request.Fields = "id, name, webViewLink, webContentLink, videoMediaMetadata";
            var file = request.Upload();


            var uploadedFile = request.ResponseBody;

            if (this.checkBox1.Checked == true)
            {
                var permission = new Google.Apis.Drive.v3.Data.Permission()
                {
                    Type = "anyone",
                    Role = "reader",
                };
                service.Permissions.Create(permission, uploadedFile.Id).Execute();
            }

            if (this.checkBox2.Checked == true)
            {
                Google.Apis.Drive.v3.Data.File file0;
                var i = 0;
                do
                {
                    FilesResource.GetRequest request0 = service.Files.Get(uploadedFile.Id);
                    request0.Fields = "id, name, videoMediaMetadata";

                    file0 = request0.Execute();

                    if (file0.VideoMediaMetadata != null)
                    {
                        Console.WriteLine("Video Finished!");
                        break;
                    }
                    else
                    {
                        textBox1.Text = $"Waited {i++ * 10} Seconds ...";
                        Application.DoEvents();
                        Thread.Sleep(10000);
                    }
                } while (true);
            }


            textBox1.Text = $"https://drive.google.com/uc?id={uploadedFile.Id}";
            textBox1.Text = uploadedFile.WebViewLink;
        }


        public static DriveService AuthenticateServiceAccount(string serviceAccountCredentialFilePath, string[] scopes)
        {

            string json = File.ReadAllText(KeyFilePath);
            JObject obj = JObject.Parse(json);
            string serviceAccountEmail = (string)obj["client_email"];

            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountCredentialFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                     .CreateScoped(scopes);
            }

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Drive Service account Authentication Sample",
            });

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textBox1.Text);
            textBox1.Text = "Copied!";
        }
    }


    public static class ClipboardNotification
    {
        public static event EventHandler ClipboardUpdate;

        private static NotificationForm form = new NotificationForm();

        private class NotificationForm : Form
        {
            public NotificationForm()
            {
                NativeMethods.SetParent(Handle, NativeMethods.HWND_MESSAGE);
                NativeMethods.AddClipboardFormatListener(Handle);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
                    ClipboardUpdate?.Invoke(null, EventArgs.Empty);
                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false);
            }
        }

        private static class NativeMethods
        {
            public const int WM_CLIPBOARDUPDATE = 0x031D;
            public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        }
    }
}
