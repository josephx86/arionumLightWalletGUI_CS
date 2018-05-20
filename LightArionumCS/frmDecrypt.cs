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
    public partial class frmDecrypt : Form {
        public frmDecrypt() {
            InitializeComponent();
        }

        private void btnDecrypt_Click(object sender, EventArgs e) {
            var res = Wallet.AES_Decrypt(Wallet.EncryptedWallet, txtPassword.Text);
            if (!res.StartsWith("arionum:")) {
                MessageBox.Show(this, "Invalid wallet password", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else {
                Wallet.DecryptedWallet = res;
                Hide();
            }
        }
    }
}
