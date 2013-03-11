using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ServiceModel;
using System.ServiceModel.Description; 

namespace WcfMessengerCl
{
    public partial class Form1 : Form
    {
        MessengerClient mClient = null;
        Dictionary<string, ChatDlg> mChatDlgDict = new Dictionary<string, ChatDlg>();
        EventHandler<ReceiveMemberListCompletedEventArgs> receiveMemberCompletedHandler = null;
        EventHandler<ReceiveConnectionCompletedEventArgs> receiveConnectionCompletedHandler = null;
        string mUserId = string.Empty;
        public string UserId
        {
            get { return mUserId; }
        }
        public Form1()
        {
            InitializeComponent();
        }

        private MessengerClient CreateClient()
        {
            System.ServiceModel.BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
            //EndpointAddress ep = new EndpointAddress("http://localhost:8000/Messenger");
            //EndpointAddress ep = new EndpointAddress("http://localhost:55560/MessengerService.svc");
            EndpointAddress ep = new EndpointAddress("http://li4shi2.azurewebsites.net/MessengerService.svc");
            return new MessengerClient(binding, ep);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            mClient = CreateClient();
            mUserId = DateTime.Now.ToString("hhmmss");
            if (mClient.Login(mUserId) == false)
            {
                this.Close();
                return;
            }
            this.Text = mUserId;
            string list = mClient.GetMemberList();
            updateMemberList(list);
            receiveMemberCompletedHandler = new EventHandler<ReceiveMemberListCompletedEventArgs>(OnReceiveMemberListCompleted);
            mClient.ReceiveMemberListCompleted += receiveMemberCompletedHandler;
            mClient.ReceiveMemberListAsync(mUserId);
            receiveConnectionCompletedHandler = new EventHandler<ReceiveConnectionCompletedEventArgs>(OnReceiveConnectionCompleted);
            mClient.ReceiveConnectionCompleted += receiveConnectionCompletedHandler;
            mClient.ReceiveConnectionAsync(mUserId);
        }

        void OnReceiveConnectionCompleted(object sender, ReceiveConnectionCompletedEventArgs e)
        {
            if(e.Error == null)
            {
                MessengerLib.ConnectionRequestData data = e.Result;
                ChatDlg dlg = new ChatDlg(this, CreateClient(), data.groupGuid, data.userSessionGuid, data.userFrom);
                mChatDlgDict.Add(data.userFrom, dlg);
                dlg.Show();
                dlg.Activate();
            }
            mClient.ReceiveConnectionAsync(mUserId);
        }

        void updateMemberList(string list)
        {
            this.listView1.Items.Clear();
            string[] members = list.Split(',');
            foreach (string member in members)
            {
                if(member != mUserId)
                    this.listView1.Items.Add(member);
            }
        }
        void OnReceiveMemberListCompleted(object sender, ReceiveMemberListCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                string list = e.Result;
                updateMemberList(list);
            }
            mClient.ReceiveMemberListAsync(mUserId);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count > 0)
            {
                ListViewItem sel = this.listView1.SelectedItems[0];
                if (mChatDlgDict.ContainsKey(sel.Text))
                {
                    // activate existing wondow
                    mChatDlgDict[sel.Text].Activate();
                }
                else
                {
                    // create new chat window
                    MessengerLib.ConnectionResultData res = mClient.RequestConnect(mUserId, sel.Text);
                    ChatDlg dlg = new ChatDlg(this, CreateClient(), res.groupGuid, res.userSessionGuid, sel.Text);
                    mChatDlgDict.Add(sel.Text, dlg);
                    dlg.Show();
                    dlg.Activate();
                }
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            mClient.ReceiveMemberListCompleted -= receiveMemberCompletedHandler;
            mClient.ReceiveConnectionCompleted -= receiveConnectionCompletedHandler;
            mClient.Logout(mUserId);
        }

    }
}
