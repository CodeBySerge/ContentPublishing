using System;
using System.Collections.Generic;

namespace ContentPublishing.Web.Models
{
    public class Response
    {
        public Response()
        {
        }

        public Response(string strStatus, string strMessage)
        {
            status = strStatus;
            message = strMessage;
        }

        public string status;
        public string message;
    }

    public class ResponseWithValue : Response
    {
        public ResponseWithValue()
        {
        }

        public ResponseWithValue(string strStatus, string strMessage)
            : base(strStatus, strMessage)
        {
        }

        public ResponseWithValue(string strStatus, string strMessage, Object objValue)
            : base(strStatus, strMessage)
        {
            value = objValue;
        }

        public Object value;
    }

    public class ResponseDictionary : Response
    {
        public ResponseDictionary()
        {
        }

        public ResponseDictionary(string strStatus, string strMessage)
            : base(strStatus, strMessage)
        {
        }

        public List<DictionaryData> list;
    }

    public class DictionaryData
    {
        public DictionaryData()
        {
        }

        public string code;
        public string label;
    }
}