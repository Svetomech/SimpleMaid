namespace SimpleMaid
{
  partial class FrmChatWindow
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      this.letterBody = new System.Windows.Forms.RichTextBox();
      this.btnSendLetter = new System.Windows.Forms.Button();
      this.btnHelpingHoof = new System.Windows.Forms.Button();
      this.btnBidFarewell = new System.Windows.Forms.Button();
      this.tmrSpikeAssistance = new System.Windows.Forms.Timer(this.components);
      this.tmrHelpingHoofBlink = new System.Windows.Forms.Timer(this.components);
      this.SuspendLayout();
      // 
      // letterBody
      // 
      this.letterBody.Enabled = false;
      this.letterBody.Location = new System.Drawing.Point(12, 12);
      this.letterBody.Name = "letterBody";
      this.letterBody.Size = new System.Drawing.Size(260, 208);
      this.letterBody.TabIndex = 0;
      this.letterBody.Text = "CelestAI: Hello, Gregory.\nYou: What?";
      this.letterBody.SelectionChanged += new System.EventHandler(this.letterBody_SelectionChanged);
      this.letterBody.KeyDown += new System.Windows.Forms.KeyEventHandler(this.letterBody_KeyDown);
      // 
      // btnSendLetter
      // 
      this.btnSendLetter.Enabled = false;
      this.btnSendLetter.Location = new System.Drawing.Point(93, 226);
      this.btnSendLetter.Name = "btnSendLetter";
      this.btnSendLetter.Size = new System.Drawing.Size(179, 23);
      this.btnSendLetter.TabIndex = 1;
      this.btnSendLetter.Text = "Send message";
      this.btnSendLetter.UseVisualStyleBackColor = true;
      this.btnSendLetter.Click += new System.EventHandler(this.btnSendLetter_Click);
      // 
      // btnHelpingHoof
      // 
      this.btnHelpingHoof.Enabled = false;
      this.btnHelpingHoof.Location = new System.Drawing.Point(64, 226);
      this.btnHelpingHoof.Name = "btnHelpingHoof";
      this.btnHelpingHoof.Size = new System.Drawing.Size(23, 23);
      this.btnHelpingHoof.TabIndex = 2;
      this.btnHelpingHoof.Text = "/)";
      this.btnHelpingHoof.UseVisualStyleBackColor = true;
      this.btnHelpingHoof.Click += new System.EventHandler(this.btnHelpingHoof_Click);
      // 
      // btnBidFarewell
      // 
      this.btnBidFarewell.Enabled = false;
      this.btnBidFarewell.Location = new System.Drawing.Point(12, 226);
      this.btnBidFarewell.Name = "btnBidFarewell";
      this.btnBidFarewell.Size = new System.Drawing.Size(46, 23);
      this.btnBidFarewell.TabIndex = 3;
      this.btnBidFarewell.Text = "Exit";
      this.btnBidFarewell.UseVisualStyleBackColor = true;
      this.btnBidFarewell.Click += new System.EventHandler(this.btnBidFarewell_Click);
      // 
      // tmrSpikeAssistance
      // 
      this.tmrSpikeAssistance.Enabled = true;
      this.tmrSpikeAssistance.Tick += new System.EventHandler(this.tmrSpikeAssistance_Tick);
      // 
      // tmrHelpingHoofBlink
      // 
      this.tmrHelpingHoofBlink.Interval = 2000;
      this.tmrHelpingHoofBlink.Tick += new System.EventHandler(this.tmrHelpingHoofBlink_Tick);
      // 
      // frmChatWindow
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(280, 257);
      this.ControlBox = false;
      this.Controls.Add(this.btnBidFarewell);
      this.Controls.Add(this.btnHelpingHoof);
      this.Controls.Add(this.btnSendLetter);
      this.Controls.Add(this.letterBody);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "FrmChatWindow";
      this.ShowIcon = false;
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "Dear Princess, ...";
      this.TopMost = true;
      this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmChatWindow_FormClosing);
      this.Load += new System.EventHandler(this.FrmChatWindow_Load);
      this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.RichTextBox letterBody;
    private System.Windows.Forms.Button btnSendLetter;
    private System.Windows.Forms.Button btnHelpingHoof;
    private System.Windows.Forms.Button btnBidFarewell;
    private System.Windows.Forms.Timer tmrSpikeAssistance;
    private System.Windows.Forms.Timer tmrHelpingHoofBlink;
  }
}