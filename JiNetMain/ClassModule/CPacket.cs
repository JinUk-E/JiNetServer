using System.Text;
using JiNet.Preview;

namespace JiNet.ClassModule
{
    public class CPacket
    {
        public CUserToken Ower { get; private set; }
        public byte[] Buffer { get; private set; }
        public int position { get; private set; }
        public int size { get; private set; }
        
        public short ProtocolID { get; private set; }
        
        public static CPacket Create(short protocolID)
        {
            var packet = new CPacket();
            packet.SetProtocol(protocolID);
            return packet;
        }
        
        public CPacket(ArraySegment<byte> buffer, CUserToken owner)
        {
            Buffer = buffer.Array;
            position = Define.Headersize;
            size = buffer.Count;
            
            ProtocolID = PopProtocolId();
            Ower = owner;
        }

        public CPacket(byte[] buffer, CUserToken owner)
        {
            Buffer = buffer;
            position = Define.Headersize;
            Ower = owner;
        }

        public CPacket()
        {
            Buffer = new byte[Define.BufferSize];
        }
        
        public short PopProtocolId()
        {
            return PopInt16();
        }
        
        public void CopyTo(CPacket packet)
        {
            packet.SetProtocol(ProtocolID);
            packet.Overwrite(Buffer, position);
        }

        private void Overwrite(byte[] source, int i)
        {
            Array.Copy(source, Buffer, source.Length);
            position = i;
        }

        public byte PopByte()
        {
            var date = Buffer[position];
            position += sizeof(byte);
            return date;
        }
        
        public short PopInt16()
        {
            var data = BitConverter.ToInt16(Buffer, position);
            position += sizeof(Int16);
            return data;
        }
        
        public int PopInt32()
        {
            var data = BitConverter.ToInt32(Buffer, position);
            position += sizeof(int);
            return data;
        }
        
        public string PopString()
        {
            var strLen = BitConverter.ToInt16(Buffer, position);
            position += sizeof(short);
            var str = Encoding.UTF8.GetString(Buffer, position, strLen);
            position += strLen;
            return str;
        }
        
        public float PopFloat()
        {
            var data = BitConverter.ToSingle(Buffer, position);
            position += sizeof(float);
            return data;
        }
        
        
        private void SetProtocol(short protocolId)
        {
            ProtocolID = protocolId;
            position = Define.Headersize;
            Push(protocolId);
        }

        public void RecordSize()
        {
            var header = BitConverter.GetBytes(position);
            header.CopyTo(Buffer, 0);
        }
        
        public void Push(short data)
        {
            var tempBuffer = BitConverter.GetBytes(data);
            tempBuffer.CopyTo(Buffer, position);
            position += tempBuffer.Length;
        }

        public void Push(byte data)
        {
            var tempBuffer = BitConverter.GetBytes((short)data);
            tempBuffer.CopyTo(Buffer, position);
            position += sizeof(byte);    
        }

        public void Push(int data)
        {
            var tempBuffer = BitConverter.GetBytes(data);
            tempBuffer.CopyTo(Buffer, position);
            position += tempBuffer.Length;
        }
        
        public void Push(string data)
        {
            var tempBuffer = Encoding.UTF8.GetBytes(data);
            var len = (short)tempBuffer.Length;
            var lenBuffer = BitConverter.GetBytes(len);
            lenBuffer.CopyTo(Buffer, position);
            position += sizeof(short);
            
            tempBuffer.CopyTo(Buffer, position);
            position += tempBuffer.Length;
        }
        
        public void Push(float data)
        {
            var tempBuffer = BitConverter.GetBytes(data);
            tempBuffer.CopyTo(Buffer, position);
            position += tempBuffer.Length;
        }
        
      
    }    
}

