using System.Data;
using System.Reflection;

private void HandleButtonClick(string passportTextbox)
{
    if (ValidateInput(passportTextbox))
        return;

    var processedPassport = ProcessPassportData(passportTextbox);

    if (processedPassport == null)
    {
        textResult.Text = "Неверный формат серии или номера паспорта";
        return;
    }

    try
    {
        var result = ExecutePassportCheck(processedPassport);
        textResult.Text = FormatResult(result, passportTextbox);
    }
    catch (SQLiteException exception)
    {
        HandleDatabaseException(exception);
    }
}

private bool ValidateInput(string input)
{
    if (string.IsNullOrWhiteSpace(input) == false)
        return false;

    MessageBox.Show("Введите серию и номер паспорта");
    return true;
}

private string ProcessPassportData(string passportData)
{
    var cleanedData = passportData.Trim().Replace(" ", string.Empty);

    return cleanedData.Length >= 10 ? cleanedData : null;
}

private DataTable ExecutePassportCheck(string passportData)
{
    using (var connection = CreateConnection())
    {
        connection.Open();
        var command = CreateCommand(connection, passportData);
        return ExecuteQuery(command);
    }
}

private SQLiteConnection CreateConnection()
{
    var dbPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "db.sqlite");

    return new SQLiteConnection($"Data Source={dbPath}");
}

private SQLiteCommand CreateCommand(SQLiteConnection connection, string passportData)
{
    var hash = Form.ComputeSha256Hash(passportData);

    return new SQLiteCommand($"select * from passports where num='{0}' limit 1;", connection);
}

private DataTable ExecuteQuery(SQLiteCommand command)
{
    DataTable dataTable = new ();
    new SQLiteDataAdapter(command).Fill(dataTable);
    return dataTable;
}

private string FormatResult(DataTable resultTable, string originalPassport)
{
    if (resultTable.Rows.Count == 0)
        return $"Паспорт «{originalPassport}» в списке участников дистанционного голосования НЕ НАЙДЕН";

    var accessGranted = Convert.ToBoolean(resultTable.Rows[0].ItemArray[1]);

    var status = accessGranted ? "ПРЕДОСТАВЛЕН" : "НЕ ПРЕДОСТАВЛЯЛСЯ";

    return $"По паспорту «{originalPassport}» доступ к бюллетеню на дистанционном электронном голосовании {status}";
}

private void HandleDatabaseException(SQLiteException exception)
{
    if (exception.ErrorCode == 1)
        MessageBox.Show("Файл db.sqlite не найден. Положите файл в папку вместе с exe.");
}
