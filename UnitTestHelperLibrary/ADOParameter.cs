using System.Data;

namespace UnitTestHelperLibrary
{
    public class ADOParameter
    {
        public string Name { get; set; }
        public SqlDbType Type { get; set; }
        public object Value { get; set; }
    }
}
