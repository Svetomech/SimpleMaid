using System;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleMaid
{
  public partial class FrmChatWindow : Form
  {
    public FrmChatWindow()
    {
      InitializeComponent();
    }

    private static string _userName;
    private static string _supportName;
    private static string _emptyLine;
    private static Color _originalColor;

    private void frmChatWindow_Load(object sender, EventArgs e)
    {
      string configUserName = ConfigurationManager.AppSettings["ChatUserName"];

      _userName = (configUserName != Variables.KeywordDefault) ? configUserName : Environment.UserName;
      _supportName = resources.SupportName;
      _emptyLine = $"{_userName}: ";
      _originalColor = btnHelpingHoof.BackColor;

      Text = $@"{Application.ProductName}: {resources.ChatWindowTitle}";
      letterBody.Text = _emptyLine;
      btnSendLetter.Text = resources.btnSendLetter_Text;
      btnBidFarewell.Text = resources.btnBidFarewell_Text;

      letterBody.Enabled = true;
      btnSendLetter.Enabled = true;
      btnBidFarewell.Enabled = true;

      UpdateCursor();
    }

    private void frmChatWindow_FormClosing(object sender, FormClosingEventArgs e)
    {
      e.Cancel = true;
      Program.ChatboxExit = true;
    }


    // TODO: Visibility change when deactivated
    private void tmrSpikeAssistance_Tick(object sender, EventArgs e)
    {
      if (Program.ChatboxExit)
        Dispose();

      // TODO: Remove this workaround (staying on top)
      TopMost = true;

      if (Program.SupportChatMessage != null)
      {
        if (String.Empty == letterBody.Text || !letterBody.Lines[letterBody.Lines.Length - 1].StartsWith(_emptyLine))
        {
          letterBody.Text += $@"{_supportName}: {Program.SupportChatMessage}{Environment.NewLine}{_emptyLine}";
        }
        else
        {
          letterBody.Text += $@"{Environment.NewLine}{_supportName}: {Program.SupportChatMessage}{Environment.NewLine}";
          letterBody.Text += letterBody.Lines[letterBody.Lines.Length - 3];
          DeleteLine(letterBody.Lines.Length - 3);
        }
        Program.SupportChatMessage = null;

        UpdateCursor();
      }

      if (Program.ChatCommand != null)
      {
        btnHelpingHoof.Enabled = true;
        tmrHelpingHoofBlink.Start();

        Program.ChatCommand = null;
      }
    }

    private void tmrHelpingHoofBlink_Tick(object sender, EventArgs e)
    {
      bool original = btnHelpingHoof.BackColor == _originalColor;

      btnHelpingHoof.BackColor = (original) ? ProfessionalColors.ButtonSelectedHighlight : _originalColor;
    }


    private void DeleteLine(int aLine)
    {
      int startIndex = letterBody.GetFirstCharIndexFromLine(aLine);
      int count = letterBody.Lines[aLine].Length;

      if (aLine < letterBody.Lines.Length - 1)
      {
        count += letterBody.GetFirstCharIndexFromLine(aLine + 1) - ((startIndex + count - 1) + 1);
      }

      letterBody.Text = letterBody.Text.Remove(startIndex, count);
    }

    private void UpdateCursor()
    {
      Activate();
      letterBody.SelectionStart = letterBody.Text.Length;
      letterBody.Focus();
    }


    private void btnSendLetter_Click(object sender, EventArgs e)
    {
      string currentLine = letterBody.Lines[letterBody.Lines.Length - 1];

      Program.UserChatMessage = currentLine.Remove(0, _emptyLine.Length);

      if (currentLine.TrimEnd() != _emptyLine.TrimEnd())
        letterBody.Text += $@"{Environment.NewLine}{_emptyLine}";

      UpdateCursor();
    }

    private void btnBidFarewell_Click(object sender, EventArgs e)
    {
      Close();
    }

    // TODO: Generalize using command parsing from awaitCommands
    private void btnHelpingHoof_Click(object sender, EventArgs e)
    {
      Program.SetUntilSet($"commands.{Program.MainConfig.MachineName}",
        Variables.AnswerPrefix + Program.RunCommand(Program.ChatCommand));

      tmrHelpingHoofBlink.Stop();
      btnHelpingHoof.Enabled = false;
    }

    private static bool _allowModification = true;

    private void letterBody_KeyDown(object sender, KeyEventArgs e)
    {
      switch(e.KeyCode)
      {
        case Keys.Enter:
          e.SuppressKeyPress = true;
          btnSendLetter.PerformClick();
          break;

        case Keys.Back:
          e.SuppressKeyPress = !_allowModification;
          break;
      }
    }

    private void letterBody_SelectionChanged(object sender, EventArgs e)
    {
      if (letterBody.SelectionLength == 0)
        _allowModification = (letterBody.SelectionStart > letterBody.GetFirstCharIndexOfCurrentLine() + _emptyLine.Length);
      else
        _allowModification = (letterBody.SelectionStart >= letterBody.GetFirstCharIndexOfCurrentLine() + _emptyLine.Length);
    }

    /*private void letterBody_MouseEnter(object sender, EventArgs e)
    {
      this.Activate();
    }*/
  }
}
