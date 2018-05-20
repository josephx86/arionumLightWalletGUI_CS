using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightArionumCS {
    public partial class frmEncryption : Form {
        public frmEncryption() {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e) {
            Hide();
        }

        private void btnEncrypt_Click(object sender, EventArgs e) {
            if (txtPassword.Text != txtConfirmPassword.Text) {
                MessageBox.Show(this, "The passwords do not match", "", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            if (txtPassword.Text.Length < 8) {
                MessageBox.Show(this, "The password must be at least 8 characters long.", "", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            Wallet.EncryptPass = txtPassword.Text;
            Wallet.IsEncrypted = true;

            Hide();
        }
    }
}
