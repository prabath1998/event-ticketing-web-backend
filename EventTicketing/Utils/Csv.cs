using System.Reflection;
using System.Text;

namespace EventTicketing.Utils
{
    public static class Csv
    {
      
        public static string Write<T>(IEnumerable<T> rows)
        {
            var sb = new StringBuilder();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
          
            sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));
            
            foreach (var r in rows)
            {
                var cells = props.Select(p => Escape(ConvertToString(p.GetValue(r))));
                sb.AppendLine(string.Join(",", cells));
            }

            return sb.ToString();
        }

        private static string ConvertToString(object? v)
        {
            if (v is null) return "";
            if (v is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
            if (v is DateTimeOffset dto) return dto.ToString("yyyy-MM-dd HH:mm:ss");
            if (v is bool b) return b ? "true" : "false";
            return v.ToString()!;
        }

        private static string Escape(string s)
        {
            if (s.IndexOfAny(['"', ',', '\n', '\r']) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}