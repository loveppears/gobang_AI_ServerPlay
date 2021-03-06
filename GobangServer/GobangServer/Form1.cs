﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace GobangServer
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// 保存连接的所有用户
        /// </summary>
        private List<Player> userList = new List<Player>(); //存放玩家们

        /// <summary>
        /// 服务器IP地址
        /// </summary>;
        private string ServerIP;

        /// <summary>
        /// 监听端口
        /// </summary>
        private int port;

        /// <summary>
        /// 监听socket
        /// </summary>
        private TcpListener myListener;

        /// <summary>
        /// 是否正常退出所有接收线程
        /// </summary>
        bool isNormalExit = false;

        public Form1()
        {
            InitializeComponent();
            SetServerIPAndPort();
            SyncConnect();
        }


        /// <summary>
        /// 获得本机IP地址和端口
        /// </summary>
        private void SetServerIPAndPort()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry localHost = Dns.GetHostEntry(hostName);
            foreach (IPAddress ips in localHost.AddressList)
            {
                if (ips.AddressFamily == AddressFamily.InterNetworkV6)
                    continue;
                else
                {
                    ServerIP = ips.ToString();
                    break;
                }
            }
            port = 23478;
        }

        private void SyncConnect()
        {
            myListener = new TcpListener(IPAddress.Parse(ServerIP), port);
            myListener.Start();
            AddItemToListBox(string.Format("开始在{0}:{1}监听客户连接", ServerIP, port));
            Thread myThread = new Thread(ListenClientConnect);
            myThread.Start();
        }


        private delegate void AddItemToListBoxDelegate(string str);
        /// <summary>
        /// 在ListBox中追加状态信息
        /// </summary>
        /// <param name="str">要追加的信息</param>
        private void AddItemToListBox(string str)
        {
            if (stateslistBox.InvokeRequired)
            {
                AddItemToListBoxDelegate d = AddItemToListBox;
                stateslistBox.Invoke(d, str);
            }
            else
            {
                stateslistBox.Items.Add(str);
                stateslistBox.SelectedIndex = stateslistBox.Items.Count - 1;
                stateslistBox.ClearSelected();
            }
        }

        /// <summary>
        /// 接收客户端连接
        /// </summary>
        private void ListenClientConnect()
        {
            TcpClient newClient = null;
            while (true)
            {
                try
                {
                    newClient = myListener.AcceptTcpClient();
                }
                catch
                {
                    //当单击‘停止监听’或者退出此窗体时 AcceptTcpClient() 会产生异常
                    //因此可以利用此异常退出循环
                    break;
                }
                //每接收一个客户端连接，就创建一个对应的线程循环接收该客户端发来的信息；
                Player user = new Player(newClient);
                Thread threadReceive = new Thread(ReceiveData);
                threadReceive.Start(user);
                userList.Add(user);
                AddItemToListBox(string.Format("[{0}]进入", newClient.Client.RemoteEndPoint));
                AddItemToListBox(string.Format("当前连接用户数：{0}", userList.Count));
            }
        }


        /// <summary>
        /// 处理接收的客户端信息
        /// </summary>
        /// <param name="userState"></param>
        private void ReceiveData(object userState)
        {
            Player user = (Player)userState;
            TcpClient client = user.client;
            while (isNormalExit == false)
            {
                string receiveString = null;
                try
                {
                    //从网络流中读出字符串，此方法会自动判断字符串长度前缀
                    receiveString = user.br.ReadString();
                }
                catch (Exception)
                {
                    if (isNormalExit == false)
                    {
                        AddItemToListBox(string.Format("与[{0}]失去联系，已终止接收该用户信息", client.Client.RemoteEndPoint));
                        RemoveUser(user);
                    }
                    break;
                }
                AddItemToListBox(string.Format("来自[{0}]：{1}", user.client.Client.RemoteEndPoint, receiveString));
                string[] splitString = receiveString.Split(',');
                switch (splitString[0])
                {
                    case "Login":
                        user.userName = splitString[1];
                        SendToAllClient(user, receiveString);
                        break;
                    case "Logout":
                        SendToAllClient(user, receiveString);
                        RemoveUser(user);
                        return;
                    case "Start":
                        AddItemToListBox(string.Format("{0}向{1}请求对战。", user.userName, splitString[1]));
                        foreach (Player target in userList)
                        {
                            if (target.userName == splitString[1])
                            {
                                SendToClient(target, "start," + user.userName );
                                break;
                            }
                        }
                        break;
                    case "OK":
                        AddItemToListBox(string.Format("{0}和{1}对战中...", user.userName, splitString[1]));
                        foreach (Player target in userList)
                        {
                            if (target.userName == splitString[1])
                            {
                                SendToClient(target, "ok," + user.userName );
                                break;
                            }
                        }
                        break;
                    case "No":
                        AddItemToListBox(string.Format("{0}拒绝和{1}对战", user.userName, splitString[1]));
                        foreach (Player target in userList)
                        {
                            if (target.userName == splitString[1])
                            {
                                SendToClient(target, "no," + user.userName);
                                break;
                            }
                        }
                        break;
                    case "Step":
                        AddItemToListBox(string.Format("{0}下在了{1},{2}", user.userName, splitString[2],splitString[3]));
                        foreach (Player target in userList)
                        {
                            if (target.userName == splitString[1])
                            {
                                SendToClient(target, "step," + splitString[2] + "," + splitString[3]);
                                break;
                            }
                        }
                        break;
                    case "Lose":
                        AddItemToListBox(string.Format("{0}打败了{1}", user.userName, splitString[1]));
                        foreach (Player target in userList)
                        {
                            if (target.userName == splitString[1])
                            {
                                SendToClient(target, "lose," + splitString[1]);
                                break;
                            }
                        }
                        break;
                    default:
                        AddItemToListBox("什么意思啊：" + receiveString);
                        break;
                }
            }
        }

        /// <summary>
        /// 移除用户
        /// </summary>
        /// <param name="user">指定要移除的用户</param>
        private void RemoveUser(Player user)
        {
            userList.Remove(user);
            user.Close();
            AddItemToListBox(string.Format("当前连接用户数：{0}", userList.Count));
        }


        /// <summary>
        /// 发送消息给所有客户
        /// </summary>
        /// <param name="user">指定发给哪个用户</param>
        /// <param name="message">信息内容</param>
        private void SendToAllClient(Player user, string message)
        {
            string command = message.Split(',')[0].ToLower();
            if (command == "login")
            {
                //获取所有客户端在线信息到当前登录用户
                for (int i = 0; i < userList.Count; i++)
                {
                    SendToClient(user, "login," + userList[i].userName);
                }
                //把自己上线，发送给所有客户端
                for (int i = 0; i < userList.Count; i++)
                {
                    if (user.userName != userList[i].userName)
                    {
                        SendToClient(userList[i], "login," + user.userName);
                    }
                }
            }
            else if (command == "logout")
            {
                for (int i = 0; i < userList.Count; i++)
                {
                    if (userList[i].userName != user.userName)
                    {
                        SendToClient(userList[i], message);
                    }
                }
            }
        }

        /// <summary>
        /// 发送 message 给 user
        /// </summary>
        /// <param name="user">指定发给哪个用户</param>
        /// <param name="message">信息内容</param>
        private void SendToClient(Player user, string message)
        {
            try
            {
                //将字符串写入网络流，此方法会自动附加字符串长度前缀
                user.bw.Write(message);
                user.bw.Flush();
                AddItemToListBox(string.Format("向[{0}]发送：{1}", user.userName, message));
            }
            catch
            {
                AddItemToListBox(string.Format("向[{0}]发送信息失败", user.userName));
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isNormalExit = true;
            for (int i = userList.Count - 1; i >= 0; i--)
            {
                RemoveUser(userList[i]);
            }
            //通过停止监听让 myListener.AcceptTcpClient() 产生异常退出监听线程
            myListener.Stop();
        }


    }
}
