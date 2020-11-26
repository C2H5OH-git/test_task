using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FindFiles
{
    public partial class Form1 : Form
    {
        private Settings Settings;

        private DateTime StartSearch;

        private bool Pause;

        private CancellationTokenSource CancellationToken = new CancellationTokenSource();

        private long FilesCount;

        private long AllFilesCount;

        private TreeNode Tree = new TreeNode();

        private string CurDir;

        Task WorkThread;
        public Form1()
        {
            InitializeComponent();
            InitSettings();
        }

        private void SaveSettings()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                using (FileStream fs = new FileStream("settings", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    formatter.Serialize(fs, Settings);
                }
            }
            catch
            {
                MessageBox.Show("Ошибка при записи настроек.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitSettings()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            Settings = new Settings();
            try
            {
                if (File.Exists("settings"))
                {
                    using (FileStream fs = new FileStream("settings", FileMode.OpenOrCreate, FileAccess.Read))
                    {
                        Settings = (Settings)formatter.Deserialize(fs);
                    }
                    DirectoryTextBox.Text = Settings.Directory;
                    FileTextBox.Text = Settings.Name;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Ошибка при чтении настроек.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DirectoryBtn_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                Settings.Directory = folderBrowserDialog1.SelectedPath;
                DirectoryTextBox.Text = Settings.Directory;
            }
        }

        private void SearchBtn_Click(object sender, EventArgs e)
        {
            if (DirectoryTextBox.Text.Length <= 0  || FileTextBox.Text.Length <= 0)
            {
                MessageBox.Show("Выберите стартовую директорию поиска и выражение для поиска", "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (WorkThread != null)
            {
                CancellationToken.Cancel();
                try
                {
                    WorkThread.Wait();
                }
                catch { }
                CancellationToken = new CancellationTokenSource();
            }

            treeView1.Nodes.Clear();
            FilesCount = 0;
            AllFilesCount = 0;

            Settings.Name = FileTextBox.Text;
            SaveSettings();

            Pause = false;
            SearchBtn.Enabled = false;
            DirectoryBtn.Enabled = false;
            CancelBtn.Enabled = true;
            CancelBtn.Text = "Остановить";

            StartSearch = DateTime.Now;
            timer1.Start();

            WorkThread =Task.Factory.StartNew(
                delegate
                {
                    FindInFiles(new DirectoryInfo(DirectoryTextBox.Text), FileTextBox.Text);
                    Invoke((Action)delegate
                    {
                        DirectoryBtn.Enabled = true;
                        SearchBtn.Enabled = true;
                        CancelBtn.Enabled = false;
                        timer1.Enabled = false;

                        TimeSpan diff = DateTime.Now - StartSearch;
                        label3.Text = "Время поиска: " + diff.ToString("%m") + " минут " + diff.ToString("%s") + " секунд";
                        label4.Text = "Текущая директория: " + CurDir;
                        label5.Text = "Файлов найдено: " + FilesCount.ToString();
                        label6.Text = "Всего файлов пройдено: " + AllFilesCount.ToString();
                    });
                }
            );
        }

        public void FindInFiles(DirectoryInfo dir, string pattern)
        {
            while (Pause)
            {
                CancellationToken.Token.ThrowIfCancellationRequested(); 
            }
            FileInfo[] files = null;
            try
            {
                files = dir.GetFiles(pattern);
            }
            catch { }
            if (files != null)
            {
                AllFilesCount += dir.GetFiles().Length;
                FilesCount += files.Length;
                CurDir = dir.FullName;
                if (files.Length > 0)
                {
                    List<DirectoryInfo> parents = new List<DirectoryInfo>() { dir };
                    while (parents.Last().Parent != null)
                    {
                        parents.Add(parents.Last().Parent);
                    }

                    string rootDir = parents.Last().Name.Replace("\\", "");
                    TreeNode node = Tree.Nodes.Count > 0 ? Tree.Nodes.Find(rootDir, false)[0] : Tree.Nodes.Add(rootDir, rootDir);
                    for (int i = parents.Count - 2; i >= 0; i--)
                    {
                        TreeNode[] temp = node.Nodes.Find(parents[i].Name, false);
                        if (temp.Length <= 0)
                        {
                            node = node.Nodes.Add(parents[i].Name, parents[i].Name);
                        }
                        else
                        {
                            node = temp[0];
                        }
                    }
                    foreach (FileInfo file in files)
                    {
                        node.Nodes.Add(file.Name, file.Name);
                    }
                }
            }

            DirectoryInfo[] dirs = null;
            try
            {
                dirs = dir.GetDirectories();
            }
            catch { }
            if (dirs != null)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    FindInFiles(subdir, pattern);
                }
            }


        }

        private void sas()
        {
            
        }

        private void CancelBtn_Click(object sender, EventArgs e)
        {
            if (CancelBtn.Text == "Остановить")
            {
                timer1.Enabled = false;
                Pause = true;
                CancelBtn.Text = "Возобновить";
                SearchBtn.Enabled = true;
                DirectoryBtn.Enabled = true;
            }
            else if(CancelBtn.Text == "Возобновить")
            {
                SearchBtn.Enabled = false;
                timer1.Enabled = true;
                Pause = false;
                DirectoryBtn.Enabled = false;
                CancelBtn.Text = "Остановить";
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan diff = DateTime.Now - StartSearch;
            label3.Text = "Время поиска: " + diff.ToString("%m") + " минут " + diff.ToString("%s") + " секунд";
            label4.Text = "Текущая директория: " + CurDir;
            label5.Text = "Файлов найдено: " + FilesCount.ToString();
            label6.Text = "Всего файлов пройдено: " + AllFilesCount.ToString();
            if (treeView1.Nodes.Count <= 0 && Tree.Nodes.Count > 0)
            {
                foreach (TreeNode node in Tree.Nodes)
                {
                    treeView1.Nodes.Add(node.Text, node.Name).Nodes.Add("");
                }
            }
            List<TreeNode> expandedNodes = СollectExpandedNodes(treeView1.Nodes);
            foreach (TreeNode node in expandedNodes)
            {
                TreeNode tempNode = GetNodeByFullPath(Tree, node.FullPath);
                if (node.Nodes.Count < tempNode.Nodes.Count)
                {
                    for (int i = 0;i < tempNode.Nodes.Count;i++)
                    {
                        if (node.Nodes.Find(tempNode.Nodes[i].Name, false).Length <= 0)
                        {
                            node.Nodes.Add(tempNode.Nodes[i].Name, tempNode.Nodes[i].Name).Nodes.Add("");
                        }
                    }
                }
            }
        }

        private TreeNode GetNodeByFullPath(TreeNode tree, string fullPath)
        {
            var names = fullPath.Split('\\').Where(x => x != "");

            TreeNode node = tree;
            foreach (string name in names)
            {
                node = node.Nodes[name];
            }
            return node;
        }

        private List<TreeNode> СollectExpandedNodes(TreeNodeCollection nodes)
        {
            List<TreeNode> lst = new List<TreeNode>();
            foreach (TreeNode checknode in nodes)
            {
                if (checknode.IsExpanded)
                    lst.Add(checknode);
                if (checknode.Nodes.Count > 0)
                    lst.AddRange(СollectExpandedNodes(checknode.Nodes));
            }
            return lst;
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            e.Node.Nodes.Clear();
            TreeNode node = GetNodeByFullPath(Tree, e.Node.FullPath);
            for (int i = 0;i < node.Nodes.Count;i++)
            {
                if(node.Nodes[i].Nodes.Count > 0)
                {
                    e.Node.Nodes.Add(node.Nodes[i].Name, node.Nodes[i].Name).Nodes.Add("");
                }
                else
                {
                    e.Node.Nodes.Add(node.Nodes[i].Name, node.Nodes[i].Name);
                }
            }
        }

        private void treeView1_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            e.Node.Nodes.Clear();
            e.Node.Nodes.Add("");
        }
    }
}
