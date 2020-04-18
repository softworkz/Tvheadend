namespace TVHeadEnd.HTSP
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text;

    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Helper;

    public class HtsMessage
    {
        public const long HTSP_VERSION = 20;
        private const byte HMF_MAP = 1;
        private const byte HMF_S64 = 2;
        private const byte HMF_STR = 3;
        private const byte HMF_BIN = 4;
        private const byte HMF_LIST = 5;

        private readonly Dictionary<string, object> dict;
        private ILogger logger;
        private byte[] data;

        public HtsMessage()
        {
            this.dict = new Dictionary<string, object>();
        }

        public void PutField(string name, object value)
        {
            if (value != null)
            {
                this.dict[name] = value;
                this.data = null;
            }
        }

        public void RemoveField(string name)
        {
            this.dict.Remove(name);
            this.data = null;
        }

        public Dictionary<string, object>.Enumerator GetEnumerator()
        {
            return this.dict.GetEnumerator();
        }

        public string Method
        {
            set
            {
                this.dict["method"] = value;
                this.data = null;
            }
            get
            {
                return this.GetString("method", "");
            }
        }

        public bool ContainsField(string name)
        {
            return this.dict.ContainsKey(name);
        }

        public BigInteger GetBigInteger(string name)
        {
            try
            {
                return (BigInteger)this.dict[name];
            }
            catch (InvalidCastException ice)
            {
                this.logger.Fatal(
                    "[TVHclient] Caught InvalidCastException for field name '" + name + "'. Expected  'System.Numerics.BigInteger' but got '" +
                    this.dict[name].GetType() + "'");
                throw ice;
            }
        }

        public long GetLong(string name)
        {
            return (long)this.GetBigInteger(name);
        }

        public long GetLong(string name, long std)
        {
            if (!this.ContainsField(name))
            {
                return std;
            }

            return this.GetLong(name);
        }

        public int GetInt(string name)
        {
            return (int)this.GetBigInteger(name);
        }

        public int GetInt(string name, int std)
        {
            if (!this.ContainsField(name))
            {
                return std;
            }

            return this.GetInt(name);
        }

        public string GetString(string name, string std)
        {
            if (!this.ContainsField(name))
            {
                return std;
            }

            return this.GetString(name);
        }

        public string GetString(string name)
        {
            object obj = this.dict[name];
            if (obj == null)
            {
                return null;
            }

            return obj.ToString();
        }

        public IList<long?> GetLongList(string name)
        {
            List<long?> list = new List<long?>();

            if (!this.ContainsField(name))
            {
                return list;
            }

            foreach (object obj in (IList)this.dict[name])
            {
                if (obj is BigInteger)
                {
                    list.Add((long)(BigInteger)obj);
                }
            }

            return list;
        }

        internal IList<long?> GetLongList(string name, IList<long?> std)
        {
            if (!this.ContainsField(name))
            {
                return std;
            }

            return this.GetLongList(name);
        }

        public IList<int?> GetIntList(string name)
        {
            List<int?> list = new List<int?>();

            if (!this.ContainsField(name))
            {
                return list;
            }

            foreach (object obj in (IList)this.dict[name])
            {
                if (obj is BigInteger)
                {
                    list.Add((int)(BigInteger)obj);
                }
            }

            return list;
        }

        internal IList<int?> GetIntList(string name, IList<int?> std)
        {
            if (!this.ContainsField(name))
            {
                return std;
            }

            return this.GetIntList(name);
        }

        public IList GetList(string name)
        {
            return (IList)this.dict[name];
        }

        public byte[] GetByteArray(string name)
        {
            return (byte[])this.dict[name];
        }

        public byte[] BuildBytes()
        {
            if (this.data != null)
            {
                return this.data;
            }

            byte[] buf = new byte[0];

            // calc data
            byte[] data = this.SerializeBinary(this.dict);

            // calc length
            int len = data.Length;
            byte[] tmpByte = new byte[1];
            tmpByte[0] = unchecked((byte)(len >> 24 & 0xFF));
            buf = buf.Concat(tmpByte).ToArray();
            tmpByte[0] = unchecked((byte)(len >> 16 & 0xFF));
            buf = buf.Concat(tmpByte).ToArray();
            tmpByte[0] = unchecked((byte)(len >> 8 & 0xFF));
            buf = buf.Concat(tmpByte).ToArray();
            tmpByte[0] = unchecked((byte)(len & 0xFF));
            buf = buf.Concat(tmpByte).ToArray();

            // append data
            buf = buf.Concat(data).ToArray();

            return buf;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\nHTSMessage:\n");
            sb.Append("  <dump>\n");
            sb.Append(this.GetValueString(this.dict, "    "));
            sb.Append("  </dump>\n\n");
            return sb.ToString();
        }

        private string GetValueString(object value, string pad)
        {
            if (value is byte[])
            {
                StringBuilder sb = new StringBuilder();
                byte[] bVal = (byte[])value;
                for (int ii = 0; ii < bVal.Length; ii++)
                {
                    sb.Append(bVal[ii]);
                    //sb.Append(" (" + Convert.ToString(bVal[ii], 2).PadLeft(8, '0') + ")");
                    sb.Append(", ");
                }

                return sb.ToString();
            }
            else if (value is IDictionary)
            {
                StringBuilder sb = new StringBuilder();
                IDictionary dictVal = (IDictionary)value;
                foreach (object key in dictVal.Keys)
                {
                    object currValue = dictVal[key];
                    sb.Append(pad + key + " : " + this.GetValueString(currValue, pad + "  ") + "\n");
                }

                return sb.ToString();
            }
            else if (value is ICollection)
            {
                StringBuilder sb = new StringBuilder();
                ICollection colVal = (ICollection)value;
                foreach (object tmpObj in colVal)
                {
                    sb.Append(this.GetValueString(tmpObj, pad) + ", ");
                }

                return sb.ToString();
            }

            return "" + value;
        }

        private byte[] SerializeBinary(IDictionary map)
        {
            byte[] buf = new byte[0];
            foreach (object key in map.Keys)
            {
                object value = map[key];
                byte[] sub = this.SerializeBinary(key.ToString(), value);
                buf = buf.Concat(sub).ToArray();
            }

            return buf;
        }

        private byte[] SerializeBinary(ICollection list)
        {
            byte[] buf = new byte[0];
            foreach (object value in list)
            {
                byte[] sub = this.SerializeBinary("", value);
                buf = buf.Concat(sub).ToArray();
            }

            return buf;
        }

        private byte[] SerializeBinary(string name, object value)
        {
            byte[] bName = this.GetBytes(name);
            byte[] bData = new byte[0];
            byte type;

            if (value is string)
            {
                type = HMF_STR;
                bData = this.GetBytes((string)value);
            }
            else if (value is BigInteger)
            {
                type = HMF_S64;
                bData = this.ToByteArray((BigInteger)value);
            }
            else if (value is int?)
            {
                type = HMF_S64;
                bData = this.ToByteArray((int)value);
            }
            else if (value is long?)
            {
                type = HMF_S64;
                bData = this.ToByteArray((long)value);
            }
            else if (value is byte[])
            {
                type = HMF_BIN;
                bData = (byte[])value;
            }
            else if (value is IDictionary)
            {
                type = HMF_MAP;
                bData = this.SerializeBinary((IDictionary)value);
            }
            else if (value is ICollection)
            {
                type = HMF_LIST;
                bData = this.SerializeBinary((ICollection)value);
            }
            else if (value == null)
            {
                throw new IOException("HTSP doesn't support null values");
            }
            else
            {
                throw new IOException("Unhandled class for " + name + ": " + value + " (" + value.GetType().Name + ")");
            }

            byte[] buf = new byte[1 + 1 + 4 + bName.Length + bData.Length];
            buf[0] = type;
            buf[1] = unchecked((byte)(bName.Length & 0xFF));
            buf[2] = unchecked((byte)(bData.Length >> 24 & 0xFF));
            buf[3] = unchecked((byte)(bData.Length >> 16 & 0xFF));
            buf[4] = unchecked((byte)(bData.Length >> 8 & 0xFF));
            buf[5] = unchecked((byte)(bData.Length & 0xFF));

            Array.Copy(bName, 0, buf, 6, bName.Length);
            Array.Copy(bData, 0, buf, 6 + bName.Length, bData.Length);

            return buf;
        }

        private byte[] ToByteArray(BigInteger big)
        {
            byte[] b = BitConverter.GetBytes((long)big);
            byte[] b1 = new byte[0];
            bool tail = false;
            for (int ii = 0; ii < b.Length; ii++)
            {
                if (b[ii] != 0 || !tail)
                {
                    tail = true;
                    b1 = b1.Concat(new byte[] { b[ii] }).ToArray();
                }
            }

            if (b1.Length == 0)
            {
                b1 = new byte[1];
            }

            return b1;
        }

        public static HtsMessage Parse(byte[] data, ILogger logger)
        {
            if (data.Length < 4)
            {
                logger.Error("[HTSMessage.parse(byte[])] Really to short");
                return null;
            }

            long len = UIntToLong(data[0], data[1], data[2], data[3]);
            //Message not fully read
            if (data.Length < len + 4)
            {
                logger.Error("[HTSMessage.parse(byte[])] not enough data for len: " + len);
                return null;
            }

            //drops 4 bytes (length information)
            byte[] messageData = new byte[len];
            Array.Copy(data, 4, messageData, 0, len);

            HtsMessage msg = DeserializeBinary(messageData);

            msg.logger = logger;
            msg.data = data;

            return msg;
        }

        public static long UIntToLong(byte b1, byte b2, byte b3, byte b4)
        {
            long i = 0;
            i <<= 8;
            i ^= b1 & 0xFF;
            i <<= 8;
            i ^= b2 & 0xFF;
            i <<= 8;
            i ^= b3 & 0xFF;
            i <<= 8;
            i ^= b4 & 0xFF;
            return i;
        }

        private static BigInteger ToBigInteger(byte[] b)
        {
            byte[] b1 = new byte[8];
            for (int ii = 0; ii < b.Length; ii++)
            {
                b1[ii] = b[ii];
            }

            long lValue = BitConverter.ToInt64(b1, 0);
            return new BigInteger(lValue);
        }

        private static HtsMessage DeserializeBinary(byte[] messageData)
        {
            byte type, namelen;
            long datalen;

            HtsMessage msg = new HtsMessage();
            int cnt = 0;

            ByteBuffer buf = new ByteBuffer(messageData);
            while (buf.HasRemaining())
            {
                type = buf.Get();
                namelen = buf.Get();
                datalen = UIntToLong(buf.Get(), buf.Get(), buf.Get(), buf.Get());

                if (buf.Length() < namelen + datalen)
                {
                    throw new IOException("Buffer limit exceeded");
                }

                //Get the key for the map (the name)
                string name = null;
                if (namelen == 0)
                {
                    name = Convert.ToString(cnt++);
                }
                else
                {
                    byte[] bName = new byte[namelen];
                    buf.Get(bName);
                    name = NewString(bName);
                }

                //Get the actual content
                object obj = null;
                byte[] bData = new byte[datalen];
                buf.Get(bData);

                switch (type)
                {
                    case HMF_STR:
                        {
                            obj = NewString(bData);
                            break;
                        }
                    case HMF_BIN:
                        {
                            obj = bData;
                            break;
                        }
                    case HMF_S64:
                        {
                            obj = ToBigInteger(bData);
                            break;
                        }
                    case HMF_MAP:
                        {
                            obj = DeserializeBinary(bData);
                            break;
                        }
                    case HMF_LIST:
                        {
                            obj = new List<object>(DeserializeBinary(bData).dict.Values);
                            break;
                        }
                    default:
                        throw new IOException("Unknown data type");
                }

                msg.PutField(name, obj);
            }

            return msg;
        }

        private static string NewString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private byte[] GetBytes(string s)
        {
            Encoding encoding = Encoding.UTF8;
            byte[] bytes = new byte[encoding.GetByteCount(s)];
            encoding.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }
    }
}