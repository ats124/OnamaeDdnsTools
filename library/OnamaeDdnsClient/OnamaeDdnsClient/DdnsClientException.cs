using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace OnamaeDdnsClient
{
    [Serializable]
    public class DdnsClientException : Exception
    {
        public DdnsClientException()
        {
        }

        public DdnsClientException(string message) : base(message)
        {
        }

        public DdnsClientException(string message, Exception inner) : base(message, inner)
        {
        }

        protected DdnsClientException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class DdnsClientCommandException : DdnsClientException
    {
        public DdnsClientCommandException(CommandResponseCode code)
            : base($"Code: {code}")
        {
            Code = code;
        }

        protected DdnsClientCommandException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
            Code = (CommandResponseCode)info.GetInt32(nameof(Code));
        }

        public CommandResponseCode Code { get; private set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Code), (int)Code);
        }
    }
}
