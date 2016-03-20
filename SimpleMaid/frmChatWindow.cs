using System;
using System.Windows.Forms;

namespace SimpleMaid
{
  public partial class frmChatWindow : Form
  {
    public frmChatWindow()
    {
      InitializeComponent();
    }

    private void frmChatWindow_Load(object sender, EventArgs e)
    {
      this.Text = $"{Application.ProductName}: {resources.ChatWindowTitle}";
      letterBody.Text = String.Empty;
      btnSendLetter.Text = resources.btnSendLetter_Text;
      btnBidFarewell.Text = resources.btnBidFarewell_Text;

      letterBody.Enabled = true;
      btnSendLetter.Enabled = true;
      btnBidFarewell.Enabled = true;
      //команды, скачка файла прямо в тексте
      //мигающее копыто
    }

    private void frmChatWindow_FormClosing(object sender, FormClosingEventArgs e)
    {
      e.Cancel = true;
      Program.ChatboxExit = true;
    }


    private static bool letterBodyFreeze = true;

    private void tmrSpikeAssistance_Tick(object sender, EventArgs e)
    {
      if (Program.ChatboxExit) this.Dispose();

      // TODO: Remove this workaround (staying on top)
      this.TopMost = true;

      if (null != Program.ChatMessage)
      {
        if (String.Empty == letterBody.Text || !letterBody.Lines[letterBody.Lines.Length - 1].StartsWith("2: "))
        {
          letterBody.Text += String.Format("1: {0}\n2: ", Program.ChatMessage);
        }
        else
        {
          letterBody.Text += String.Format("\n1: {0}\n", Program.ChatMessage);
          letterBody.Text += letterBody.Lines[letterBody.Lines.Length - 3];
          deleteLine(letterBody.Lines.Length - 3);
        }
        Program.ChatMessage = null;

        this.Activate();
        letterBody.SelectionStart = letterBody.Text.Length;
        letterBody.Focus();
      }
    }

    private void deleteLine(int aLine)
    {
      int startIndex = letterBody.GetFirstCharIndexFromLine(aLine);
      int count = letterBody.Lines[aLine].Length;

      // Eat new line chars
      if (aLine < letterBody.Lines.Length - 1)
      {
        count += letterBody.GetFirstCharIndexFromLine(aLine + 1) -
            ((startIndex + count - 1) + 1);
      }

      letterBody.Text = letterBody.Text.Remove(startIndex, count);
    }


    private void btnSendLetter_Click(object sender, EventArgs e)
    {
      //move to new line if not already
      //write You: 
    }

    private void letterBody_KeyPress(object sender, KeyPressEventArgs e)
    {
      if (e.KeyChar == (char)Keys.Enter)
      {
        btnSendLetter.PerformClick();
      }
    }

    private void btnBidFarewell_Click(object sender, EventArgs e)
    {
      this.Close();
    }

    private void btnHelpingHoof_Click(object sender, EventArgs e)
    {
      
    }
  }
}
