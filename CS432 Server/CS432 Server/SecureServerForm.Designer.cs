namespace CS432_Server
{
    partial class SecureServerForm
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
            this.listView_Users = new System.Windows.Forms.ListView();
            this.label_listViewUsers = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label_infoDisplayLabel = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label = new System.Windows.Forms.Label();
            this.button_StartServer = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listView_Users
            // 
            this.listView_Users.Location = new System.Drawing.Point(451, 64);
            this.listView_Users.Name = "listView_Users";
            this.listView_Users.Size = new System.Drawing.Size(121, 335);
            this.listView_Users.TabIndex = 0;
            this.listView_Users.UseCompatibleStateImageBehavior = false;
            // 
            // label_listViewUsers
            // 
            this.label_listViewUsers.AutoSize = true;
            this.label_listViewUsers.Location = new System.Drawing.Point(483, 48);
            this.label_listViewUsers.Name = "label_listViewUsers";
            this.label_listViewUsers.Size = new System.Drawing.Size(67, 13);
            this.label_listViewUsers.TabIndex = 1;
            this.label_listViewUsers.Text = "Online Users";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(21, 162);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(424, 237);
            this.textBox1.TabIndex = 2;
            // 
            // label_infoDisplayLabel
            // 
            this.label_infoDisplayLabel.AutoSize = true;
            this.label_infoDisplayLabel.Location = new System.Drawing.Point(18, 146);
            this.label_infoDisplayLabel.Name = "label_infoDisplayLabel";
            this.label_infoDisplayLabel.Size = new System.Drawing.Size(32, 13);
            this.label_infoDisplayLabel.TabIndex = 3;
            this.label_infoDisplayLabel.Text = "INFO";
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(21, 64);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(81, 20);
            this.textBox2.TabIndex = 4;
            // 
            // label
            // 
            this.label.AutoSize = true;
            this.label.Location = new System.Drawing.Point(21, 47);
            this.label.Name = "label";
            this.label.Size = new System.Drawing.Size(26, 13);
            this.label.TabIndex = 5;
            this.label.Text = "Port";
            // 
            // button_StartServer
            // 
            this.button_StartServer.Location = new System.Drawing.Point(123, 61);
            this.button_StartServer.Name = "button_StartServer";
            this.button_StartServer.Size = new System.Drawing.Size(75, 23);
            this.button_StartServer.TabIndex = 6;
            this.button_StartServer.Text = "Start Server";
            this.button_StartServer.UseVisualStyleBackColor = true;
            // 
            // SecureServerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 411);
            this.Controls.Add(this.button_StartServer);
            this.Controls.Add(this.label);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.label_infoDisplayLabel);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.label_listViewUsers);
            this.Controls.Add(this.listView_Users);
            this.Name = "SecureServerForm";
            this.Text = "SecureServerForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listView_Users;
        private System.Windows.Forms.Label label_listViewUsers;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label_infoDisplayLabel;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Label label;
        private System.Windows.Forms.Button button_StartServer;
    }
}