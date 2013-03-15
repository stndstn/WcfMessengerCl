using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ServiceModel;
using System.IO;
using System.Threading;

namespace WcfMessengerCl
{
    public partial class ChatDlg : Form
    {
        private MessengerClient mClient = null;
        public MessengerClient MessageClient
        {
            get { return mClient; }
        }
        private string mDestHostPort = "";
        public string DestHostPort
        {
            get { return mDestHostPort; }
        }
        private Guid mUserSessionGuid = Guid.Empty;
        public Guid UserSessionGuid
        {
            get { return mUserSessionGuid; }
        }
        private Guid mGroupSessionGuid = Guid.Empty;
        public Guid GroupSessionGuid
        {
            get { return mGroupSessionGuid; }
        }
        private string mUserId;
        Microsoft.Ink.InkPicture mInkPicture = null;
        List<Point[]> mOriginalPts = null;
        int mFittingError = 100;
        private Form mParent = null;
        private byte[] mBgImgData = null;
        /*
        EventHandler<ReceiveMessageCompletedEventArgs> mReceiveMessageCompletedHandler = null;
        EventHandler<ReceiveStrokeCompletedEventArgs> mReceiveStorokeCompetedHandler = null;
        EventHandler<ReceiveBGImgChunkCompletedEventArgs> mReceiveBGImgChunkCompletedHandler = null;
         */
        EventHandler<ReceiveContentDataCompletedEventArgs> mReceiveContentDataCompletedHandler = null;

        public ChatDlg(Form parent, MessengerClient client, Guid groupSessionGuid, Guid userSessionGuid, string userToConnect)
        {
            mInkPicture = new Microsoft.Ink.InkPicture();
            mOriginalPts = new List<Point[]>();
            mClient = client;
            mUserSessionGuid = userSessionGuid;
            mGroupSessionGuid = groupSessionGuid;
            mParent = parent;
            
            InitializeComponent();
            this.toolStrip1.Items.Add(this.toolStripButton1);
            this.toolStrip1.Items.Add(this.toolStripButton2);

            this.Text = userToConnect;
            button1.Enabled = false;
            this.splitContainer1.Panel1.Controls.Add(mInkPicture);
            mInkPicture.Dock = DockStyle.Fill;
            //mInkPicture.Width = this.ClientSize.Width;
            //mInkPicture.Height = this.ClientSize.Height;
            //GetXYIndexes(ref PACKET_IDX_PtX, ref PACKET_IDX_PtY);   //Save the X and Y data locations within the packet data.
            mInkPicture.Stroke += new Microsoft.Ink.InkCollectorStrokeEventHandler(InkPicture_Stroke);
            /*
            mReceiveMessageCompletedHandler = new EventHandler<ReceiveMessageCompletedEventArgs>(OnReceiveMessageCompleted);
            mClient.ReceiveMessageCompleted += mReceiveMessageCompletedHandler;
            mReceiveStorokeCompetedHandler = new EventHandler<ReceiveStrokeCompletedEventArgs>(OnReceiveStrokeCompleted);
            mClient.ReceiveStrokeCompleted += mReceiveStorokeCompetedHandler;
            mReceiveBGImgChunkCompletedHandler = new EventHandler<ReceiveBGImgChunkCompletedEventArgs>(OnReceiveBGImgChunkCompleted);
            mClient.ReceiveBGImgChunkCompleted += mReceiveBGImgChunkCompletedHandler;
             */
            mReceiveContentDataCompletedHandler = new EventHandler<ReceiveContentDataCompletedEventArgs>(OnReceiveContentDataCompleted);
            mClient.ReceiveContentDataCompleted += mReceiveContentDataCompletedHandler;
            Form1 form1 = (Form1)parent;
            mUserId = form1.UserId;
            /*
            mClient.ReceiveMessageAsync(mUserSessionGuid);
            mClient.ReceiveStrokeAsync(mUserSessionGuid);
            mClient.ReceiveBGImgChunkAsync(mUserSessionGuid);
             */
            mClient.ReceiveContentDataAsync(mUserSessionGuid);

            string debug = string.Format("{0} - {1}", mUserId, userToConnect);
            WriteLine(debug, Color.Black);
            debug = string.Format("Group {0}", groupSessionGuid.ToString());
            WriteLine(debug, Color.Black);
            debug = string.Format("Group {0}", userSessionGuid.ToString());
            WriteLine(debug, Color.Black);
        }

        private void ClearAll()
        {
            mBgImgData = null;
            mInkPicture.Ink.DeleteStrokes();
            mInkPicture.BackgroundImage = null;
            mInkPicture.Refresh();
        }
        void OnReceiveContentDataCompleted(object sender, ReceiveContentDataCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                this.Visible = true;

                MessengerLib.ContentData data = e.Result;
                if (data.groupGuid != Guid.Empty)
                {
                    switch(data.type)
                    {
                        case MessengerLib.DataType.Message:
                            string buf = string.Format("{0}>{1}", data.userid, data.message);
                            WriteLine(buf, Color.Red);
                            break;
                        case MessengerLib.DataType.Stroke:
                            int count = data.x.Length;
                            Point[] pt = new Point[count];
                            for (int i = 0; i < count; i++)
                            {
                                pt[i] = new Point(data.x[i], data.y[i]);
                            }
                            mInkPicture.Ink.CreateStroke(pt);
                            mInkPicture.Refresh();
                            break;
                        case MessengerLib.DataType.BGImgCk:
                            if (data.total == 0 && data.groupGuid != Guid.Empty)
                            {
                                // clear data
                                ClearAll();
                                mClient.ReceiveContentDataAsync(mUserSessionGuid);
                                return;
                            }

                            if (data.offset == 0)
                            {
                                mBgImgData = new byte[data.total];
                            }
                            for (int i = 0; i < data.len; i++)
                            {
                                mBgImgData[data.offset + i] = data.data[i];
                            }
                            if (data.offset + data.len == data.total)
                            {
                                string fname = string.Format("{0}_screen_receive.png", this.mUserId);
                                FileStream fsw = File.OpenWrite(fname);
                                int len = mBgImgData.Length;
                                fsw.Write(mBgImgData, 0, len);
                                fsw.Close();
                                FileStream fsr = File.OpenRead(fname);
                                Image pngImage = Image.FromStream(fsr);
                                fsr.Close();
                                mInkPicture.Ink.DeleteStrokes();
                                mInkPicture.BackgroundImage = pngImage;
                                mInkPicture.BackgroundImageLayout = ImageLayout.None;
                                mInkPicture.Refresh();
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                WriteLine(e.Error.Message, Color.Black);
            }
            Thread.Sleep(500);
            mClient.ReceiveContentDataAsync(mUserSessionGuid);
        }
        /*
        void OnReceiveBGImgChunkCompleted(object sender, ReceiveBGImgChunkCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                MessengerLib.BGImgChunk data = e.Result;
                if (data.groupGuid != Guid.Empty)
                {
                    if (data.total == 0 && data.groupGuid != Guid.Empty)
                    {
                        // clear data
                        ClearAll();
                        mClient.ReceiveBGImgChunkAsync(mUserSessionGuid);
                        return;
                    }

                    if (data.offset == 0)
                    {
                        mBgImgData = new byte[data.total];
                    }
                    for (int i = 0; i < data.len; i++)
                    {
                        mBgImgData[data.offset + i] = data.data[i];
                    }
                    if (data.offset + data.len == data.total)
                    {
                        string fname = string.Format("{0}_screen_receive.png", this.mUserId);
                        FileStream fsw = File.OpenWrite(fname);
                        int len = mBgImgData.Length;
                        fsw.Write(mBgImgData, 0, len);
                        fsw.Close();
                        FileStream fsr = File.OpenRead(fname);
                        Image pngImage = Image.FromStream(fsr);
                        fsr.Close();
                        mInkPicture.Ink.DeleteStrokes();
                        mInkPicture.BackgroundImage = pngImage;
                        mInkPicture.BackgroundImageLayout = ImageLayout.None;
                        mInkPicture.Refresh();
                    }
                }
            }
            mClient.ReceiveBGImgChunkAsync(mUserSessionGuid);
        }
        */
        /*
        void OnReceiveStrokeCompleted(object sender, ReceiveStrokeCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                MessengerLib.StrokeData data = e.Result;
                if (data.groupGuid != Guid.Empty)
                {
                    int count = data.x.Length;
                    Point[] pt = new Point[count];
                    for (int i = 0; i < count; i++)
                    {
                        pt[i] = new Point(data.x[i], data.y[i]);
                    }
                    mInkPicture.Ink.CreateStroke(pt);
                    mInkPicture.Refresh();
                }
            }
            mClient.ReceiveStrokeAsync(mUserSessionGuid);
        }
        */
        /*
        void  OnReceiveMessageCompleted(object sender, ReceiveMessageCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                MessengerLib.MessageData data = e.Result;
                if (data.groupGuid != Guid.Empty)
                {
                    string buf = string.Format("{0}>{1}", data.userid, data.message);
                    WriteLine(buf, Color.Red);
                }
            }
            else
            {
                WriteLine("timeout", Color.Black);
            }
            mClient.ReceiveMessageAsync(mUserSessionGuid);
        }
        */
        private void WriteLine(string s, Color c)
        {
            int pos1 = this.textMessages.TextLength;
            string str = string.Format("{0}\r\n", s);
            this.textMessages.AppendText(str);
            int pos2 = this.textMessages.TextLength;
            int len = pos2 - pos1;
            this.textMessages.Select(pos1, len);
            this.textMessages.SelectionColor = c;
            this.textMessages.Select(pos2, 0);
        }
        /*
        public void OnMessageReceived(object sender, MessegerLib.Message. e)
        {
            if (e.id == mId)
            {
                WriteLine(e.msg, Color.Red);
            }
        }
         */
        private void AddStroke(int[] x, int[] y)
        {
            int count = x.Length;
            Point[] pt = new Point[count];
            for (int i = 0; i < count; i++ )
            {
                pt[i] = new Point(x[i], y[i]);
            }
            mInkPicture.Ink.CreateStroke(pt);
            mInkPicture.Refresh();
        }
        /*
        public void OnStrokeReceived(object sender, MessegerLib.Message.StrokeReceivedEventArgs e)
        {
            if (e.id == mId)
            {
                AddStroke(e.x, e.y);
            }
        }
         */
        /*
        public void OnBGImgReceived(object sender, MessegerLib.Message.BGImgReceivedEventArgs e)
        {
            if (e.id == mId)
            {
                FileStream fsw = File.OpenWrite("screen_receive.png");
                int len = e.data.Length;
                fsw.Write(e.data, 0, len);
                fsw.Close();
                FileStream fsr = File.OpenRead("screen_receive.png");
                Image pngImage = Image.FromStream(fsr);
                fsr.Close();
                mInkPicture.BackgroundImage = pngImage;
                mInkPicture.BackgroundImageLayout = ImageLayout.None;
            }
        }
         */
        void InkPicture_Stroke(object sender, Microsoft.Ink.InkCollectorStrokeEventArgs e)
        {
            if (mFittingError >= 0)
            {
                Point[] pt = e.Stroke.GetFlattenedBezierPoints(mFittingError);
                Microsoft.Ink.Stroke st = mInkPicture.Ink.CreateStroke(pt);
                mInkPicture.Ink.DeleteStroke(e.Stroke);
                //mInkPicture.Ink.Strokes.Add(st);
                mInkPicture.Refresh();
                int count = pt.Length;
                //MessengerLib.StrokeData data = new MessengerLib.StrokeData();
                MessengerLib.ContentData data = new MessengerLib.ContentData();
                data.type = MessengerLib.DataType.Stroke;
                data.groupGuid = mGroupSessionGuid;
                data.userid = mUserId;
                data.x = new int[count];
                data.y = new int[count];
                for(int i = 0; i < count; i++)
                {
                    data.x[i] = pt[i].X;
                    data.y[i] = pt[i].Y;
                }
                //mClient.SendStroke(data);
                mClient.SendContentData(data);
            }
        }

        private void Send()
        {
            //MessengerLib.MessageData data = new MessengerLib.MessageData();
            MessengerLib.ContentData data = new MessengerLib.ContentData();
            data.type = MessengerLib.DataType.Message;
            data.groupGuid = mGroupSessionGuid;
            data.message = textSend.Text;
            data.userid = mUserId;
            //mClient.SendMessage(data);
            mClient.SendContentData(data);
            string buf = string.Format("{0}>{1}", data.userid, data.message);
            WriteLine(buf, Color.Blue);
            textSend.Clear();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            Send();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = (textSend.Text.Length > 0) ? true : false;
        }

        private void textSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (textSend.Text.Length > 0)
                {
                    Send();
                }
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            ClearAll();
            // send empty image
            //MessengerLib.BGImgChunk data = new MessengerLib.BGImgChunk();
            MessengerLib.ContentData data = new MessengerLib.ContentData();
            data.type = MessengerLib.DataType.BGImgCk;
            data.len = 0;
            data.offset = 0;
            data.total = 0;
            data.data = null;
            data.userid = mUserId;
            data.groupGuid = mGroupSessionGuid;
            //mClient.SendBGImgChunk(data);
            mClient.SendContentData(data);
        }
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            mParent.WindowState = FormWindowState.Minimized;
            CopyDesktopScreen cds = new CopyDesktopScreen();
            cds.ShowDialog();
            Bitmap bmp = cds.ScreenShotBmp;
            mInkPicture.Ink.DeleteStrokes();
            mInkPicture.BackgroundImage = bmp;
            mInkPicture.BackgroundImageLayout = ImageLayout.None;
            string fname = string.Format("{0}_screen_send.png", this.mUserId);
            FileStream fsw = File.OpenWrite(fname);
            bmp.Save(fsw, System.Drawing.Imaging.ImageFormat.Png);
            fsw.Close();

            FileStream fsr = File.OpenRead(fname);
            //MessengerLib.BGImgChunk data = new MessengerLib.BGImgChunk();
            MessengerLib.ContentData data = new MessengerLib.ContentData();
            data.type = MessengerLib.DataType.BGImgCk;
            data.len = 0;
            data.offset = 0;
            data.total = (int)fsr.Length;
            data.data = new byte[1024];
            data.userid = mUserId;
            data.groupGuid = mGroupSessionGuid;
            while (data.offset < data.total)
            {
                data.len = fsr.Read(data.data, 0, 1024);
                //mClient.SendBGImgChunk(data);
                mClient.SendContentData(data);
                data.offset += data.len;
            }
            fsr.Close();
        }

        private void ChatDlg_FormClosed(object sender, FormClosedEventArgs e)
        {
            /*
            mClient.ReceiveMessageCompleted -= mReceiveMessageCompletedHandler;
            mClient.ReceiveStrokeCompleted -= mReceiveStorokeCompetedHandler;
            mClient.ReceiveBGImgChunkCompleted -= mReceiveBGImgChunkCompletedHandler;
             */
            mClient.ReceiveContentDataCompleted -= mReceiveContentDataCompletedHandler;
        }

        private void ChatDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
    }
}
