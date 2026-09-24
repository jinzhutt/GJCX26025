using Microsoft.Data.Sqlite;

var src = args[0];
var dst = args[1];
Console.WriteLine($"Copy Type: {src} -> {dst}");

using var cSrc = new SqliteConnection("Data Source=" + src);
using var cDst = new SqliteConnection("Data Source=" + dst);
cSrc.Open();
cDst.Open();

using (var cmd = cSrc.CreateCommand())
{
    cmd.CommandText = "SELECT ID, Name, TypeID, mark, CreateTime, UpdateTime, IsDeleted FROM [Type]";
    using var r = cmd.ExecuteReader();
    var n = 0;
    while (r.Read())
    {
        var name = r.GetString(1);
        using var check = cDst.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM [Type] WHERE Name=$n";
        check.Parameters.AddWithValue("$n", name);
        var exists = Convert.ToInt64(check.ExecuteScalar()) > 0;
        if (exists) { Console.WriteLine("skip exists: " + name); continue; }

        using var ins = cDst.CreateCommand();
        ins.CommandText = @"INSERT INTO [Type] (Name, TypeID, mark, CreateTime, UpdateTime, IsDeleted)
            VALUES ($name, $typeId, $mark, $ct, $ut, $del)";
        ins.Parameters.AddWithValue("$name", name);
        ins.Parameters.AddWithValue("$typeId", r.IsDBNull(2) ? 1 : r.GetInt32(2));
        ins.Parameters.AddWithValue("$mark", r.IsDBNull(3) ? (object)DBNull.Value : r.GetValue(3));
        ins.Parameters.AddWithValue("$ct", r.IsDBNull(4) ? (object)DBNull.Value : r.GetValue(4));
        ins.Parameters.AddWithValue("$ut", r.IsDBNull(5) ? (object)DBNull.Value : r.GetValue(5));
        ins.Parameters.AddWithValue("$del", r.IsDBNull(6) ? (object)DBNull.Value : r.GetValue(6));
        ins.ExecuteNonQuery();
        n++;
        Console.WriteLine("inserted: " + name);
    }
    Console.WriteLine("inserted count=" + n);
}

using (var cmd = cDst.CreateCommand())
{
    cmd.CommandText = "SELECT ID, Name FROM [Type] ORDER BY ID";
    using var r = cmd.ExecuteReader();
    while (r.Read()) Console.WriteLine($"dst: {r[0]} | {r[1]}");
}
