using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ledger.db");
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

InitDb(conn);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== 记账本 ===");
    Console.WriteLine("1. 记一笔");
    Console.WriteLine("2. 查看今天");
    Console.WriteLine("3. 查看本月");
    Console.WriteLine("4. 查看全部");
    Console.WriteLine("5. 月度统计");
    Console.WriteLine("6. 删除记录");
    Console.WriteLine("0. 退出");
    Console.Write("选择: ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1": AddExpense(conn); break;
        case "2": QueryByDate(conn, DateTime.Today); break;
        case "3": QueryByMonth(conn, DateTime.Today); break;
        case "4": QueryAll(conn); break;
        case "5": MonthlyStats(conn); break;
        case "6": DeleteExpense(conn); break;
        case "0": return;
        default: Console.WriteLine("无效选项"); break;
    }
}

static void InitDb(SqliteConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Expenses (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Amount      REAL NOT NULL,
            Category    TEXT NOT NULL,
            Description TEXT,
            Date        TEXT NOT NULL,
            CreatedAt   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        )";
    cmd.ExecuteNonQuery();
}

static void AddExpense(SqliteConnection conn)
{
    Console.Write("金额 (元): ");
    var amountStr = Console.ReadLine()?.Trim();
    if (!decimal.TryParse(amountStr, out var amount) || amount <= 0)
    {
        Console.WriteLine("❌ 金额无效");
        return;
    }

    Console.Write("分类 (餐饮/交通/购物/娱乐/居住/医疗/教育/其他): ");
    var category = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(category)) category = "其他";

    Console.Write("备注 (可选): ");
    var description = Console.ReadLine()?.Trim() ?? "";

    Console.Write("日期 (回车=今天, 格式 yyyy-MM-dd): ");
    var dateStr = Console.ReadLine()?.Trim();
    DateTime date;
    if (string.IsNullOrEmpty(dateStr))
        date = DateTime.Today;
    else if (!DateTime.TryParse(dateStr, out date))
    {
        Console.WriteLine("❌ 日期格式无效");
        return;
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "INSERT INTO Expenses (Amount, Category, Description, Date) VALUES (@amount, @category, @desc, @date)";
    cmd.Parameters.AddWithValue("@amount", (double)amount);
    cmd.Parameters.AddWithValue("@category", category);
    cmd.Parameters.AddWithValue("@desc", description);
    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
    cmd.ExecuteNonQuery();

    Console.WriteLine($"✅ 已记录: {amount}元 [{category}] {description}");
}

static void QueryByDate(SqliteConnection conn, DateTime date)
{
    var dateStr = date.ToString("yyyy-MM-dd");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM Expenses WHERE Date = @date ORDER BY CreatedAt DESC";
    cmd.Parameters.AddWithValue("@date", dateStr);
    PrintExpenses(cmd, dateStr);
}

static void QueryByMonth(SqliteConnection conn, DateTime date)
{
    var monthStr = date.ToString("yyyy-MM");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM Expenses WHERE Date LIKE @month ORDER BY Date DESC, CreatedAt DESC";
    cmd.Parameters.AddWithValue("@month", $"{monthStr}%");
    PrintExpenses(cmd, monthStr);
}

static void QueryAll(SqliteConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT * FROM Expenses ORDER BY Date DESC, CreatedAt DESC";
    PrintExpenses(cmd, "全部");
}

static void PrintExpenses(SqliteCommand cmd, string title)
{
    using var reader = cmd.ExecuteReader();
    Console.WriteLine($"--- {title} 记录 ---");
    Console.WriteLine($"{"ID",-5} {"日期",-12} {"金额",8} {"分类",-8} 备注");
    Console.WriteLine(new string('-', 50));

    var total = 0m;
    var count = 0;
    while (reader.Read())
    {
        var amount = (decimal)reader.GetDouble(1);
        total += amount;
        count++;
        var desc = reader.IsDBNull(4) ? "" : reader.GetString(4);
        Console.WriteLine($"{reader.GetInt32(0),-5} {reader.GetString(3),-12} {amount,8:F2} {reader.GetString(2),-8} {desc}");
    }

    Console.WriteLine(new string('-', 50));
    Console.WriteLine($"共 {count} 笔，合计 {total:F2} 元");
}

static void MonthlyStats(SqliteConnection conn)
{
    Console.Write("月份 (回车=本月, 格式 yyyy-MM): ");
    var monthStr = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(monthStr))
        monthStr = DateTime.Today.ToString("yyyy-MM");

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT Category, COUNT(*) as Cnt, SUM(Amount) as Total 
        FROM Expenses 
        WHERE Date LIKE @month 
        GROUP BY Category 
        ORDER BY Total DESC";
    cmd.Parameters.AddWithValue("@month", $"{monthStr}%");

    using var reader = cmd.ExecuteReader();
    Console.WriteLine($"--- {monthStr} 月度统计 ---");
    Console.WriteLine($"{"分类",-8} {"笔数",6} {"合计",10} 占比");
    Console.WriteLine(new string('-', 40));

    var grandTotal = 0m;
    var categories = new List<(string cat, int cnt, decimal total)>();

    while (reader.Read())
    {
        var cat = reader.GetString(0);
        var cnt = reader.GetInt32(1);
        var total = (decimal)reader.GetDouble(2);
        grandTotal += total;
        categories.Add((cat, cnt, total));
    }

    foreach (var (cat, cnt, total) in categories)
    {
        var pct = grandTotal > 0 ? total / grandTotal * 100 : 0;
        Console.WriteLine($"{cat,-8} {cnt,6} {total,10:F2} {pct,5:F1}%");
    }

    Console.WriteLine(new string('-', 40));
    Console.WriteLine($"{"合计",-8} {categories.Sum(c => c.cnt),6} {grandTotal,10:F2}");
}

static void DeleteExpense(SqliteConnection conn)
{
    Console.Write("输入要删除的记录 ID: ");
    var idStr = Console.ReadLine()?.Trim();
    if (!int.TryParse(idStr, out var id))
    {
        Console.WriteLine("❌ ID 无效");
        return;
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Amount, Category, Description, Date FROM Expenses WHERE Id = @id";
    cmd.Parameters.AddWithValue("@id", id);
    using var reader = cmd.ExecuteReader();
    if (!reader.Read())
    {
        Console.WriteLine("❌ 未找到该记录");
        return;
    }

    var amount = reader.GetDouble(0);
    var category = reader.GetString(1);
    var desc = reader.IsDBNull(2) ? "" : reader.GetString(2);
    var date = reader.GetString(3);

    Console.WriteLine($"确认删除: {date} {amount}元 [{category}] {desc} (y/n)? ");
    var confirm = Console.ReadLine()?.Trim().ToLower();
    if (confirm != "y") { Console.WriteLine("已取消"); return; }

    cmd.Parameters.Clear();
    cmd.CommandText = "DELETE FROM Expenses WHERE Id = @id";
    cmd.Parameters.AddWithValue("@id", id);
    var deleted = cmd.ExecuteNonQuery();
    Console.WriteLine(deleted > 0 ? "✅ 已删除" : "❌ 删除失败");
}
