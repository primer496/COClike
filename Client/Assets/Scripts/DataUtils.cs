namespace DevelopersHub.ClashOfWhatecer
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    // XML + GZip 辅助方法用于在客户端和服务端之间传输较大的游戏状态数据。
    public static partial class Data
    {
        /// <summary>
        /// 将字符串编码为 Base64 文本。
        /// </summary>
        public static string EncodeString(string input)
        {
            try
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            }
            catch (Exception)
            {
                return input;
            }
        }

        /// <summary>
        /// 将 Base64 文本解码为原始字符串。
        /// </summary>
        public static string DecodeString(string input)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(input));
            }
            catch (Exception)
            {
                return input;
            }
        }

        /// <summary>
        /// 检查邮箱地址格式是否有效。
        /// </summary>
        public static bool IsEmailValid(string email)
        {
            email = email.Trim();
            if (email.EndsWith(".")) { return false; }
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch { return false; }
        }

        /// <summary>
        /// 生成指定长度的纯数字随机验证码。
        /// </summary>
        public static string RandomCode(int length)
        {
            if (length <= 0)
            {
                return "";
            }
            Random random = new Random();
            const string chars = "0123456789";
            string value = "";
            while (value.Length < length)
            {
                value += chars[random.Next(0, chars.Length)].ToString();
            }
            return value;
        }

        /// <summary>
        /// 计算字符串的 MD5 哈希值。
        /// </summary>
        public static string EncrypteToMD5(string data)
        {
            UTF8Encoding ue = new UTF8Encoding();
            byte[] bytes = ue.GetBytes(data);
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(bytes);
            string hashString = "";
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashString = hashString + Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
            }
            return hashString.PadLeft(32, '0');
        }

        /// <summary>
        /// 通过序列化与反序列化创建对象的深拷贝。
        /// </summary>
        public static T CloneClass<T>(this T target)
        {
            return Desrialize<T>(Serialize<T>(target));
        }

        /// <summary>
        /// 将对象序列化为 XML 字符串。
        /// </summary>
        public static string Serialize<T>(this T target)
        {
            XmlSerializer xml = new XmlSerializer(typeof(T));
            StringWriter writer = new StringWriter();
            xml.Serialize(writer, target);
            return writer.ToString();
        }

        /// <summary>
        /// 将 XML 字符串反序列化为对象。
        /// </summary>
        public static T Desrialize<T>(this string target)
        {
            XmlSerializer xml = new XmlSerializer(typeof(T));
            StringReader reader = new StringReader(target);
            return (T)xml.Deserialize(reader);
        }

        /// <summary>
        /// 异步将对象序列化为 XML 字符串。
        /// </summary>
        public async static Task<string> SerializeAsync<T>(this T target)
        {
            Task<string> task = Task.Run(() =>
            {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringWriter writer = new StringWriter();
                xml.Serialize(writer, target);
                return writer.ToString();
            });
            return await task;
        }

        /// <summary>
        /// 异步将 XML 字符串反序列化为对象。
        /// </summary>
        public async static Task<T> DesrializeAsync<T>(this string target)
        {
            Task<T> task = Task.Run(() =>
            {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringReader reader = new StringReader(target);
                return (T)xml.Deserialize(reader);
            });
            return await task;
        }

        /// <summary>
        /// 将源流的数据复制到目标流。
        /// </summary>
        public static void CopyTo(Stream source, Stream target)
        {
            byte[] bytes = new byte[4096]; int count;
            while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
            {
                target.Write(bytes, 0, count);
            }
        }

        /// <summary>
        /// 将字符串压缩为 GZip 字节数组。
        /// </summary>
        public static byte[] Compress(string target)
        {
            var bytes = Encoding.UTF8.GetBytes(target);
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(mso, CompressionMode.Compress))
                    {
                        CopyTo(msi, gs);
                    }
                    return mso.ToArray();
                }
            }
        }

        /// <summary>
        /// 异步压缩字符串为 GZip 字节数组。
        /// </summary>
        public async static Task<byte[]> CompressAsync(string target)
        {
            Task<byte[]> task = Task.Run(() =>
            {
                return Compress(target);
            });
            return await task;
        }

        /// <summary>
        /// 将 GZip 字节数组解压为字符串。
        /// </summary>
        public static string Decompress(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                    {
                        CopyTo(gs, mso);
                    }
                    return Encoding.UTF8.GetString(mso.ToArray());
                }
            }
        }

        /// <summary>
        /// 异步解压 GZip 字节数组为字符串。
        /// </summary>
        public async static Task<string> DecompressAsync(byte[] bytes)
        {
            Task<string> task = Task.Run(() =>
            {
                return Decompress(bytes);
            });
            return await task;
        }
    }
}
