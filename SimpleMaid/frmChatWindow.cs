using System;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;

namespace SimpleMaid
{
  public partial class frmChatWindow : Form
  {
    public frmChatWindow()
    {
      InitializeComponent();
    }

    private static string userName;
    private static string supportName;
    private static string emptyLine;
    private static Color originalColor;

    private void frmChatWindow_Load(object sender, EventArgs e)
    {
      string configUserName = ConfigurationManager.AppSettings["ChatUserName"];

      userName = (configUserName != Variables.KeywordDefault) ? configUserName : Environment.UserName;
      supportName = resources.SupportName;
      emptyLine = $"{userName}: ";
      originalColor = btnHelpingHoof.BackColor;

      this.Text = $"{Application.ProductName}: {resources.ChatWindowTitle}";
      letterBody.Text = emptyLine;
      btnSendLetter.Text = resources.btnSendLetter_Text;
      btnBidFarewell.Text = resources.btnBidFarewell_Text;

      letterBody.Enabled = true;
      btnSendLetter.Enabled = true;
      btnBidFarewell.Enabled = true;

      updateCursor();
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
        this.Dispose();

      // TODO: Remove this workaround (staying on top)
      this.TopMost = true;

      if (Program.SupportChatMessage != null)
      {
        if (String.Empty == letterBody.Text || !letterBody.Lines[letterBody.Lines.Length - 1].StartsWith(emptyLine))
        {
          letterBody.Text += $"{supportName}: {Program.SupportChatMessage}\n{emptyLine}";
        }
        else
        {
          letterBody.Text += $"\n{supportName}: {Program.SupportChatMessage}\n";
          letterBody.Text += letterBody.Lines[letterBody.Lines.Length - 3];
          deleteLine(letterBody.Lines.Length - 3);
        }
        Program.SupportChatMessage = null;

        updateCursor();
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
      bool original = btnHelpingHoof.BackColor == originalColor;

      btnHelpingHoof.BackColor = (original) ? ProfessionalColors.ButtonSelectedHighlight : originalColor;
    }


    private void deleteLine(int aLine)
    {
      int startIndex = letterBody.GetFirstCharIndexFromLine(aLine);
      int count = letterBody.Lines[aLine].Length;

      if (aLine < letterBody.Lines.Length - 1)
      {
        count += letterBody.GetFirstCharIndexFromLine(aLine + 1) - ((startIndex + count - 1) + 1);
      }

      letterBody.Text = letterBody.Text.Remove(startIndex, count);
    }

    private void updateCursor()
    {
      this.Activate();
      letterBody.SelectionStart = letterBody.Text.Length;
      letterBody.Focus();
    }


    private void btnSendLetter_Click(object sender, EventArgs e)
    {
      string currentLine = letterBody.Lines[letterBody.Lines.Length - 1];

      Program.UserChatMessage = currentLine.Remove(0, emptyLine.Length);

      if (currentLine.TrimEnd() != emptyLine.TrimEnd())
        letterBody.Text += $"\n{emptyLine}";

      updateCursor();
    }

    private void btnBidFarewell_Click(object sender, EventArgs e)
    {
      this.Close();
    }

    // TODO: Generalize using command parsing from awaitCommands
    private void btnHelpingHoof_Click(object sender, EventArgs e)
    {
      Program.SetUntilSet($"commands.{Program.mainConfig.MachineName}",
        Variables.AnswerPrefix + Program.executeCommand(Program.ChatCommand));

      tmrHelpingHoofBlink.Stop();
      btnHelpingHoof.Enabled = false;
    }

    private static bool allowModification = true;

    private void letterBody_KeyDown(object sender, KeyEventArgs e)
    {
      switch(e.KeyCode)
      {
        case Keys.Enter:
          e.SuppressKeyPress = true;
          btnSendLetter.PerformClick();
          break;

        case Keys.Back:
          e.SuppressKeyPress = !allowModification;
          break;
      }
    }

    private void letterBody_SelectionChanged(object sender, EventArgs e)
    {
      if (letterBody.SelectionLength == 0)
        allowModification = (letterBody.SelectionStart > letterBody.GetFirstCharIndexOfCurrentLine() + emptyLine.Length);
      else
        allowModification = (letterBody.SelectionStart >= letterBody.GetFirstCharIndexOfCurrentLine() + emptyLine.Length);
    }

    /*private void letterBody_MouseEnter(object sender, EventArgs e)
    {
      this.Activate();
    }*/
  }
}
