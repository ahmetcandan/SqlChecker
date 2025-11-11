namespace SqlChecker;

public partial class FrmSettings : Form
{
    private readonly Settings _settings;
    public Settings Settings => _settings;

    public FrmSettings(Settings? settings = null)
    {
        InitializeComponent();
        _settings = settings ?? new();
        DialogResult = DialogResult.Cancel;
    }

    private async void BtnSave_Click(object sender, EventArgs e)
    {
        _settings.ConnectionString = txtConnectionString.Text;
        await File.WriteAllTextAsync("settings.json", System.Text.Json.JsonSerializer.Serialize(_settings));
        DialogResult = DialogResult.OK;
        this.Close();
    }

    private void FrmSettings_Load(object sender, EventArgs e)
    {
        txtConnectionString.Text = _settings?.ConnectionString;
    }
}
