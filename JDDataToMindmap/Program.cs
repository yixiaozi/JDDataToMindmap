using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace JDDataToMindmap
{
    class Program
    {
        static void Main(string[] args)
        {
            //DataTable的标题为"id","parent_id","level","name","merger_name"
            DataTable dt = new DataTable();
            dt = ReadCSV(args[0]);
            System.Xml.XmlDocument x = new XmlDocument();
            string mindmap = args[1];
            x.Load(mindmap);
            //为dt添加列标记是否已添加到xml中,默认值为false
            dt.Columns.Add("isAdd", typeof(bool));
            foreach (DataRow dr in dt.Rows)
            {
                dr["isAdd"] = false;
            }
            //一直遍历DataTable中isAdd为false的数据，直到所有数据已添加，如果parent_id为空则为一级分类，然后根据parent_id确定二级分类，以此类推,将DataTable的结构用AddTaskToFilexml方法添加到x
            while (dt.Select("isAdd=false").Length > 0)
            {
                foreach (DataRow dr in dt.Select("isAdd=false"))
                {
                    if (dr["parent_id"].ToString() == "") 
                    {
                        AddTaskToFile(x, dr["parent_id"].ToString(), dr["name"].ToString(),dr["id"].ToString(),dr["level"].ToString());
                        dr["isAdd"] = true;
                    }
                    else
                    {
                        try
                        {
                            if (dt.Select("id='" + dr["parent_id"].ToString()+"\'").Length > 0)//确保有父级
                            {
                                if (dt.Select("id='" + dr["parent_id"].ToString()+"\'")[0]["isAdd"].ToString() == "True")
                                {
                                    AddTaskToFile(x, dr["parent_id"].ToString(), dr["name"].ToString(), dr["id"].ToString(),dr["level"].ToString());
                                    dr["isAdd"] = true;
                                }
                            }
                            else
                            {
                                //Console.WriteLine("没有父级:" + dr["name"].ToString());
                                //打印所有未添加的数量
                                Console.WriteLine(dt.Select("isAdd=false").Length);
                            }
                        }
                        catch (Exception ex)
                        {
                            dr["isAdd"] = true;
                        }
                    }
                }
            }
            x.Save(mindmap);
            ConvertFile(mindmap);
        }
        //尝试将字符串转换成数字再转换成字符串，如果转换失败则返回原来值
        public static string TryParse(string str)
        {
            try
            {
                return Convert.ToInt32(str).ToString();
            }
            catch (Exception)
            {
                return str;
            }
        }
        public static DataTable ReadCSV(string filePath)
        {
            //读取csv文件到DateSet
            DataTable dt = new DataTable();
            string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
            string[] firstLine = lines[0].Split(',');
            foreach (string column in firstLine)
            {
                //column去掉双引号
                dt.Columns.Add(column.Replace("\"", ""), typeof(string));
            }
            for (int i = 1; i < lines.Length; i++)
            {
                string[] data = lines[i].Split(',');
                DataRow dr = dt.NewRow();
                
                for (int j = 0; j < data.Length; j++)
                {
                    try { dr[j] = TryParse(data[j].Replace("\"","")); }
                    catch { }
                }
                dt.Rows.Add(dr);
            }
            return dt;
        }
        
        public static void AddTaskToFile(XmlDocument x, string parent_id, string taskName,string taskID,string level)
        {
            if (taskName == "")
            {
                return;
            }
            XmlNode root = x.GetElementsByTagName("node").Cast<XmlNode>().First(m => m.Attributes["TEXT"] != null && m.Attributes["TEXT"].Value != "");
            if (parent_id!="")
            {
                root = x.GetElementsByTagName("node").Cast<XmlNode>().First(m => m.Attributes[0].Name == "TEXT"&& m.Attributes["FenleiID"]!=null&& m.Attributes["FenleiID"].Value == parent_id&& m.Attributes["Level"]!=null&& m.Attributes["Level"].Value == (Convert.ToInt16(level)-1).ToString());
            }
            XmlNode newNote = x.CreateElement("node");
            XmlAttribute newNotetext = x.CreateAttribute("TEXT");
            newNotetext.Value = taskName;
            XmlAttribute newNoteCREATED = x.CreateAttribute("CREATED");
            newNoteCREATED.Value = (Convert.ToInt64((DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalMilliseconds)).ToString();
            XmlAttribute newNoteMODIFIED = x.CreateAttribute("MODIFIED");
            newNoteMODIFIED.Value = (Convert.ToInt64((DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalMilliseconds)).ToString();
            newNote.Attributes.Append(newNotetext);
            newNote.Attributes.Append(newNoteCREATED);
            newNote.Attributes.Append(newNoteMODIFIED);
            XmlAttribute TASKID = x.CreateAttribute("ID");
            newNote.Attributes.Append(TASKID);
            newNote.Attributes["ID"].Value = Guid.NewGuid().ToString();
            XmlAttribute Level = x.CreateAttribute("Level");
            newNote.Attributes.Append(Level);
            newNote.Attributes["Level"].Value = level;
            XmlAttribute FenleiID = x.CreateAttribute("FenleiID");
            newNote.Attributes.Append(FenleiID);
            newNote.Attributes["FenleiID"].Value = taskID;
            root.AppendChild(newNote);
        }
        public static void ConvertFile(string path)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                string text = "";
                using (StreamReader textStream = file.OpenText())
                {
                    text = textStream.ReadToEnd();
                }
                text = text.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n", "");
                text = ConvertString(text);
                try
                {
                    File.WriteAllText(path, text);
                }
                catch (Exception)
                {
                    //MessageBox.Show("Don't be quickly");
                }
            }
            catch (Exception)
            {
            }
        }
        public static string ConvertString(string str)
        {
            IEnumerable<string> col = Regex.Matches(str, @"[\u4e00-\u9fbb|\u3002\uff1b\uff0c\uff01\uff1a\u201c\u201d\uff08\uff09\u3001\u300c\uff1f\u300a\u300b\u300d\u300e\u300f\u2018\u2019\u3014\u3015\u3010\u3011\u2014\u2026\u2013\uff0e\u3008\u3009]").OfType<Match>().Select(m => m.Groups[0].Value).Distinct();
            foreach (string item in col)
            {
                str = str.Replace(item, Fallback(item[0]));
            }
            return str;
        }
        public static string Fallback(char charUnknown)
        {
            string d = string.Format(CultureInfo.InvariantCulture, "&#x{0:X};", new object[]
                {
                    (int)charUnknown
                });
            return d;
        }
    }
}
