using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Floodgate.Exceptions
{
    public class LoopException : Exception
    {
        public LoopException()
        {
        }

        public LoopException(string message) : base(message)
        {
        }

        public LoopException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected LoopException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
