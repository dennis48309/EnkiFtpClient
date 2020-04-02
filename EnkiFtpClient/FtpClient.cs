using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace EnkiFtpClient
{
    public enum DataTransferType { DownloadList, UploadFile, UploadFileArray, DownloadFile };

    public class FtpClient
    {
        DataTransferType dataTransferType;

        StreamWriter sWriter;
        StreamReader sReader;
        NetworkStream nStream;
        NetworkStream nStreamData;
        TcpClient client;
        TcpClient clientData;

        public byte[] downloadedBytes;
        public byte[] toUploadBytes;

        Thread thrFtpData;
        string readData = null;

        string hostname;
        string username;
        string password;
        int passivePort;

        public string currentWorkingDirectory = @"/";

        public FtpClient(string hostname, string username, string password)
        {
            this.hostname = hostname;
            this.username = username;
            this.password = password;
            client = new TcpClient(hostname, 21);
            client.ReceiveTimeout = 0;
            nStream = client.GetStream();
            sWriter = new StreamWriter(nStream);
            sReader = new StreamReader(nStream);
        }

        public bool Login()
        {
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) != "220")
            {
                return false;
            }
            SendMessage(@"USER " + username);
            List<string> lines2 = ReceiveResponse();
            Debug.WriteLine(lines2[lines2.Count - 1]);
            if (lines2[lines2.Count - 1].Substring(0, 3) != "331")
            {
                return false;
            }
            SendMessage(@"PASS " + password);
            List<string> lines3 = ReceiveResponse();
            Debug.WriteLine(lines3[lines3.Count - 1]);
            if (lines3[lines3.Count - 1].Substring(0, 3) != "230")
            {
                return false;
            }
            return true;
        }

        private void SendMessage(string msg)
        {
            sWriter.Write(msg + (char)0x0d + (char)0x0a);
            sWriter.Flush();
        }

        public bool MakeDirectory(string name)
        {
            SendMessage("MKD " + name);
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "257")
            {
                return true;
            }
            return false;
        }

        public bool DeleteFolder(string name)
        {
            SendMessage("RMD " + name);
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "250")
            {
                return true;
            }
            return false;
        }

        public bool DeleteFile(string name)
        {
            SendMessage("DELE " + name);
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "250")
            {
                return true;
            }
            return false;
        }

        public void Disconnect()
        {
            SendMessage("QUIT");
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "221")
            {

            }
            if (client != null)
            {
                client.Close();
            }
            if (clientData != null)
            {
                clientData.Close();
            }
        }

        public bool PrintWorkingDirectory()
        {
            SendMessage("PWD");
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "257")
            {
                return true;
            }
            return false;
        }

        int fileSize = 0;
        public bool Size(string filename)
        {
            SendMessage("SIZE " + filename);
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "213")
            {
                string[] split = lines[lines.Count - 1].Split(' ');
                fileSize = int.Parse(split[1]);
                return true;
            }
            return false;
        }

        public bool ChangeWorkingDirectory(string path)
        {
            SendMessage("CWD " + path);
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "250")
            {
                Regex regex = new Regex(@"(?<=" + (char)34 + @").+(?=" + (char)34 + @")");
                Match match = regex.Match(lines[lines.Count - 1]);
                currentWorkingDirectory = match.Value;
                return true;
            }
            return false;
        }

        public bool Retrieve(string filePath, string filename)
        {
            pathOfFile = filePath;
            Passive();
            SendMessage("RETR " + filename);
            dataTransferType = DataTransferType.DownloadFile;
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) != "150")
            {
                return false;
            }

            List<string> lines2 = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines2[lines2.Count - 1].Substring(0, 3) == "226")
            {
                return true;
            }
            return false;
        }

        string pathOfFile;
        public bool Store(string filePath, string filename)
        {
            pathOfFile = filePath;
            Passive();
            SendMessage("STOR " + filename);
            dataTransferType = DataTransferType.UploadFile;
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) != "150")
            {
                return false;
            }

            List<string> lines2 = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines2[lines2.Count - 1].Substring(0, 3) == "226")
            {
                return true;
            }
            return false;
        }
        public bool Store(string filename)
        {
            Passive();
            SendMessage("STOR " + filename);
            dataTransferType = DataTransferType.UploadFileArray;
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) != "150")
            {
                return false;
            }

            List<string> lines2 = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines2[lines2.Count - 1].Substring(0, 3) == "226")
            {
                return true;
            }
            return false;
        }

        public void Passive()
        {
            SendMessage("PASV");
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "227")
            {
                Regex regex = new Regex(@"(?<=\().+(?=\))");
                Match match = regex.Match(lines[lines.Count - 1]);
                string[] split = match.Value.Split(',');
                passivePort = int.Parse(split[4]) * 256 + int.Parse(split[5]);

                clientData = new TcpClient(hostname, passivePort);
                thrFtpData = new Thread(new ThreadStart(HandleData));
                thrFtpData.Start();
            }
        }

        public List<string> ListPrint()
        {
            List<string> newList = new List<string>();

            Passive();
            SendMessage(@"LIST -al");
            dataTransferType = DataTransferType.DownloadList;
            ReceiveResponsePrint();
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "226")
            {
                while (readData == null)
                {

                }
                string[] splitFiles = readData.Split('\n');
                foreach (string s in splitFiles)
                {
                    if (s != "")
                    {
                        newList.Add(s);
                    }
                }
            }
            return newList;
        }

        public List<FtpDirectory> GetDirectories()
        {
            List<FtpDirectory> tempList = new List<FtpDirectory>();

            Regex regex = new Regex(@"(?<= )[^ ]+?(?= )");
            Regex regexTimeName = new Regex(@"(?<=:.. ).+");
            Regex regexTime = new Regex(@"(?<= )[0-9][0-9]:[0-9][0-9](?= )");
            Regex regexNonTimeName = new Regex(@"(?<=[0-9]  [0-9][0-9][0-9][0-9] ).+");

            string[] stringSeparators = new string[] { "\r\n" };
            string[] splitFiles = readData.Split(stringSeparators, StringSplitOptions.None);
            foreach (string sTemp in splitFiles)
            {
                if (sTemp != "")
                {
                    string s = sTemp.Replace("\r\n", "");
                    if (s.Substring(0, 1) == "d")
                    {
                        FtpDirectory newDir = new FtpDirectory();
                        Match mTime = regexTime.Match(s);
                        if (mTime.Success)
                        {
                            Match mTimeName = regexTimeName.Match(s);
                            newDir.name = mTimeName.Value.Trim();
                        }
                        else
                        {
                            Match match = regexNonTimeName.Match(s);
                            newDir.name = match.Value.Trim();
                        }

                        MatchCollection m = regex.Matches(" " + s + " ");

                        newDir.attributes = m[0].Value;
                        tempList.Add(newDir);
                    }
                }
            }
            return tempList;
        }

        public List<FtpFile> GetFiles()
        {
            List<FtpFile> tempList = new List<FtpFile>();

            Regex regex = new Regex(@"(?<= )[^ ]+?(?= )");
            Regex regexTimeName = new Regex(@"(?<=:.. ).+");
            Regex regexTime = new Regex(@"(?<= )[0-9][0-9]:[0-9][0-9](?= )");
            Regex regexNonTimeName = new Regex(@"(?<=[0-9]  [0-9][0-9][0-9][0-9] ).+");

            string[] stringSeparators = new string[] { "\r\n" };
            string[] splitFiles = readData.Split(stringSeparators, StringSplitOptions.None);
            foreach (string sTemp in splitFiles)
            {
                if (sTemp != "")
                {
                    string s = sTemp.Replace("\r\n", "");
                    if (s.Substring(0, 1) != "d")
                    {
                        FtpFile newFile = new FtpFile();
                        Match mTime = regexTime.Match(s);
                        if (mTime.Success)
                        {
                            Match mTimeName = regexTimeName.Match(s);
                            newFile.name = mTimeName.Value.Trim();
                        }
                        else
                        {
                            Match match = regexNonTimeName.Match(s);
                            newFile.name = match.Value.Trim();
                        }

                        MatchCollection m = regex.Matches(" " + s + " ");

                        newFile.attributes = m[0].Value;
                        tempList.Add(newFile);
                    }
                }
            }
            return tempList;
        }

        public void List()
        {
            readData = null;
            Passive();
            SendMessage(@"LIST -al");
            dataTransferType = DataTransferType.DownloadList;
            ReceiveResponsePrint();
            List<string> lines = ReceiveResponse();
            Debug.WriteLine(lines[lines.Count - 1]);
            if (lines[lines.Count - 1].Substring(0, 3) == "226")
            {
                while (readData == null)
                {

                }
            }
        }

        private bool FileExists(string name)
        {
            foreach (FtpFile f in GetFiles())
            {
                if (f.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private bool DirectoryExists(string name)
        {
            foreach (FtpDirectory d in GetDirectories())
            {
                if (d.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        int bufferSize = 1238;
        private void UploadFile()
        {
            nStreamData = clientData.GetStream();
            BinaryWriter writer = new BinaryWriter(nStreamData);
            FileInfo fInfo = new FileInfo(pathOfFile);
            using (FileStream fStream = new FileStream(pathOfFile, FileMode.Open))
            {
                int totalBytesToSend = (int)fInfo.Length;
                while (true)
                {
                    if (totalBytesToSend == 0)
                    {
                        break;
                    }
                    else if (totalBytesToSend < bufferSize)
                    {
                        byte[] allBytesOfFile = new byte[totalBytesToSend];
                        fStream.Read(allBytesOfFile, 0, totalBytesToSend);
                        writer.Write(allBytesOfFile);
                        writer.Flush();
                        totalBytesToSend = 0;
                    }
                    else
                    {
                        byte[] allBytesOfFile = new byte[bufferSize];
                        fStream.Read(allBytesOfFile, 0, bufferSize);
                        writer.Write(allBytesOfFile);
                        writer.Flush();
                        totalBytesToSend -= bufferSize;
                    }
                }
            }
            clientData.Close();
        }

        private void UploadFileArray()
        {
            nStreamData = clientData.GetStream();
            BinaryWriter writer = new BinaryWriter(nStreamData);
            using (MemoryStream fStream = new MemoryStream(toUploadBytes))
            {
                int totalBytesToSend = toUploadBytes.Length;
                while (true)
                {
                    if (totalBytesToSend == 0)
                    {
                        break;
                    }
                    else if (totalBytesToSend < bufferSize)
                    {
                        byte[] tempArray = new byte[totalBytesToSend];
                        fStream.Read(tempArray, 0, totalBytesToSend);
                        writer.Write(tempArray);
                        writer.Flush();
                        totalBytesToSend = 0;
                    }
                    else
                    {
                        byte[] tempArray = new byte[bufferSize];
                        fStream.Read(tempArray, 0, bufferSize);
                        writer.Write(tempArray);
                        writer.Flush();
                        totalBytesToSend -= bufferSize;
                    }
                }
            }
            clientData.Close();
        }

        private void DownloadFile()
        {
            nStreamData = clientData.GetStream();
            BinaryReader bReader = new BinaryReader(nStreamData);
            FileInfo fInfo = new FileInfo(pathOfFile);
            downloadedBytes = bReader.ReadBytes(fileSize);
            clientData.Close();
        }

        private void HandleData()
        {
            if (dataTransferType == DataTransferType.UploadFile)
            {
                UploadFile();
            }
            else if (dataTransferType == DataTransferType.DownloadList)
            {
                DownloadData();
            }
            else if (dataTransferType == DataTransferType.DownloadFile)
            {
                DownloadFile();
            }
            else if (dataTransferType == DataTransferType.UploadFileArray)
            {
                UploadFileArray();
            }
        }

        private void DownloadData()
        {
            nStreamData = clientData.GetStream();
            StreamReader reader = new StreamReader(nStreamData);
            readData = reader.ReadToEnd();
        }

        private List<string> ReceiveResponse()
        {
            List<string> lines = new List<string>();
            while (true)
            {
                string line = sReader.ReadLine();
                lines.Add(line);
                string fourthChar = line.Substring(3, 1);
                if (fourthChar == " ")
                    break;
            }
            return lines;
        }

        private void ReceiveResponsePrint()
        {
            while (true)
            {
                string line = sReader.ReadLine();
                string fourthChar = line.Substring(3, 1);
                if (fourthChar == " ")
                {
                    Debug.WriteLine(line);
                    break;
                }
            }
        }
    }
}
