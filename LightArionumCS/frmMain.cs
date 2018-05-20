using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LightArionumCS {
    public partial class frmMain : Form {
        private BackgroundWorker syncWorker = new BackgroundWorker();

        public frmMain()
        {
            InitializeComponent();
            syncWorker.DoWork += syncWorker_DoWork;
            Load += FrmMain_Load;
        }

        private void syncWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            sync_data();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;

            if (!Directory.Exists(Wallet.DataPath)) {
                Directory.CreateDirectory(Wallet.DataPath);
            }
            if (!File.Exists(Wallet.WalletPath)) {
                // Generate new wallet and the first address
                Wallet.generate_keys();
                if (Wallet.PrivateKey.Length < 20) {
                    MessageBox.Show(this, "Could not generate the keys", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }
                string wallet = "arionum:" + Wallet.PrivateKey + ":" + Wallet.PublicKey;
                DialogResult response = MessageBox.Show(this, "A new wallet has been generated. Would you like to encrypt this wallet?", "Encryption", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (response == DialogResult.Yes) {
                    new frmEncryption().ShowDialog();
                    if (Wallet.EncryptPass.Length < 8) {
                        MessageBox.Show(this, "Could not encrypt the wallet. Exiting...", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.Exit(0);
                    }
                    wallet = Wallet.AES_Encrypt(wallet, Wallet.EncryptPass);
                }

                try {
                    using (var file = new StreamWriter(Wallet.WalletPath, false)) {
                        file.WriteLine(wallet);
                        file.Close();
                    }
                } catch {
                    MessageBox.Show(this, "Could not write the wallet file. Please check the permissions on " + Wallet.WalletPath, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(0);
                }
            } else {
                // Importing the wallet
                Wallet.IsEncrypted = false;
                var s = "";
                using (var reader = new StreamReader(Wallet.WalletPath)) {
                    s = reader.ReadToEnd();
                    reader.Close();
                }

                if (!s.StartsWith("arionum:")) {
                    Wallet.EncryptedWallet = s;
                    new frmDecrypt().ShowDialog();
                    s = Wallet.DecryptedWallet;
                    if (s.Length == 0) {
                        MessageBox.Show(this, "Could not decrypt wallet. Exiting...", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.Exit(0);
                    }
                    Wallet.IsEncrypted = true;
                }

                var wal = s.Split(':');
                Wallet.PrivateKey = wal[1];
                Wallet.PublicKey = wal[2];
            }

            Wallet.PublicKey = Wallet.PublicKey.Trim();
            Wallet.PrivateKey = Wallet.PrivateKey.Trim();
            txtPublicKey.Text = Wallet.PublicKey;
            txtPrivateKey.Text = Wallet.PrivateKey;
            var amount = 1.0m;
            var fee = amount * Wallet.RATE;
            txtSendAmount.Text = amount.ToString("N2");
            lblFee.Text = fee.ToString("N8");
            if (Wallet.IsEncrypted) {
                btnDecrypt.Text = "Decrypt";
            } else {
                btnDecrypt.Text = "Encrypt"; ;
            }
            var encoder = new UTF8Encoding();
            var sha512hasher = new SHA512Managed();
            var enc = encoder.GetBytes(Wallet.PublicKey);
            for (int i = 0; i <= 8; i++) {
                enc = sha512hasher.ComputeHash(enc);
            }

            Wallet.Address = SimpleBase.Base58.Bitcoin.Encode(enc);
            txtAddress.Text = Wallet.Address;
            var peer_data = Wallet.http_get("http://api.arionum.com/peers.txt");
            // tmp = RegularExpressions.Regex.Split(peer_data, Environment.NewLine)
            var arg = new string[] { "\r\n", "\n" };
            var tmp = peer_data.Split(arg, StringSplitOptions.None);
            peer_data = "";

            Wallet.TotalPeers = 0;
            for (int i = 0; i < tmp.Length; i++) {
                if (Wallet.TotalPeers > (Wallet.Peers.Length - 1)) {
                    break;
                }
                var t = tmp[i].Trim();
                if (!string.IsNullOrEmpty(t)) {
                    Wallet.Peers[Wallet.TotalPeers] = t;
                    Wallet.TotalPeers++;
                }
            }

            var peersPath = Wallet.DataPath + "\\peers.txt";
            if (Wallet.TotalPeers > 10) {
                peer_data = string.Join("\r\n", Wallet.Peers).Trim();
                try {
                    using (var file = new StreamWriter(peersPath, false)) {
                        file.Write(peer_data);
                        file.Close();
                    }
                } catch {

                }
            } else {
                var s = "";
                using (var tr = new StreamReader(peersPath)) {
                    s = tr.ReadToEnd();
                    tr.Close();
                }
                tmp = s.Split(arg, StringSplitOptions.None);
                peer_data = "";
                Wallet.TotalPeers = 0;
                for (int i = 0; i < tmp.Length; i++) {
                    if (Wallet.TotalPeers > (Wallet.Peers.Length - 1)) {
                        break;
                    }

                    var t = tmp[i].Trim();
                    if (!string.IsNullOrEmpty(t)) {
                        Wallet.Peers[Wallet.TotalPeers] = t;
                        Wallet.TotalPeers++;
                    }
                }
            }

            syncWorker.RunWorkerAsync();
        }

        public void sync_data()
        {
            try {
                if (Wallet.SyncErr > 5) {
                    Wallet.SyncErr = 0;
                    return;
                }
                var Generator = new Random();
                var r = Generator.Next(0, Wallet.TotalPeers - 1);
                var peer = Wallet.Peers[r];
                var res = Wallet.get_json(peer + "/api.php?q=getPendingBalance&account=" + Wallet.Address);

                if (res.Equals("")) {
                    Wallet.SyncErr++;
                    sync_data();
                    return;
                }
                decimal.TryParse(res, out decimal bal);
                Wallet.Balance = bal;
                lblBalance.Text = "Balance: " + Wallet.Balance.ToString("N8");
                lblStatusNode.Text = peer;
                res = Wallet.get_json(peer + "/api.php?q=currentBlock"); ;
                if (string.IsNullOrEmpty(res)) {
                    return;
                }
                var dataObject = Newtonsoft.Json.Linq.JObject.Parse(res);
                var height = dataObject.GetValue("height").ToString();
                lblCurrentBlock.Text = height;

                res = Wallet.get_json(peer + "/api.php?q=getTransactions&account=" + Wallet.Address);
                if (string.IsNullOrEmpty(res)) {
                    return;
                }
                var transactions = Newtonsoft.Json.Linq.JArray.Parse(res);

                dataGridView1.Rows.Clear();
                foreach (var x in transactions.Children()) {
                    var obj = x as Newtonsoft.Json.Linq.JObject;
                    double.TryParse(obj.GetValue("date").ToString(), out var nTimestamp);
                    DateTime nDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                    nDateTime = nDateTime.AddSeconds(nTimestamp);
                    dataGridView1.Rows.Add(
                        nDateTime.ToString("MM/dd/yyyy HH:mm"),
                        obj.GetValue("type").ToString(),
                        obj.GetValue("val").ToString(),
                        obj.GetValue("fee").ToString(),
                        obj.GetValue("confirmations").ToString(),
                        obj.GetValue("src").ToString(),
                        obj.GetValue("dst").ToString(),
                        obj.GetValue("message").ToString(),
                        obj.GetValue("id").ToString()
                       );
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void btnSendTransaction_Click(object sender, EventArgs e)
        {
            var recepient = txtSendTo.Text.Trim();
            if (recepient.Length < 10) {
                return;
            }
            decimal.TryParse(txtSendAmount.Text.Trim(), out var sum);
            if (sum < Wallet.MINIMUM) {
                MessageBox.Show(this, "Invalid amount", "", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            var fee = sum * Wallet.RATE;
            if (fee < Wallet.MINIMUM) {
                fee = Wallet.MINIMUM;
            }
            if (Wallet.Balance < (fee + sum)) {
                MessageBox.Show(this, "Not enough balance to send this transaction!", "", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            var confirmationQuestions = $"Are you sure you wish to send {sum} ARO to {recepient} ?";
            var response = MessageBox.Show(this, confirmationQuestions, "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (response == DialogResult.Yes) {
                var tmp_key = Wallet.coin2pem(Wallet.PrivateKey, true);
                var tmp_key2 = Wallet.coin2pem(Wallet.PublicKey);

                var textReader = new StringReader(tmp_key) as TextReader;
                var pemReader = new PemReader(textReader);
                var _keyPair = pemReader.ReadObject() as AsymmetricCipherKeyPair;
                var _privateKeyParams = _keyPair.Private as ECPrivateKeyParameters;
                var _publicKeyParams = _keyPair.Public as ECPublicKeyParameters;

                var uTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                var info = $"{sum.ToString("N8")}-{fee.ToString("N8")}-{recepient}-{txtSendMessage.Text}-1-{Wallet.PublicKey}-{uTime.ToString()}";
                var signer = SignerUtilities.GetSigner("SHA-256withECDSA"); ;
                signer.Init(true, _keyPair.Private);
                var bytes = Encoding.UTF8.GetBytes(info);
                signer.BlockUpdate(bytes, 0, bytes.Length);
                var signature = signer.GenerateSignature();
                var sig = SimpleBase.Base58.Bitcoin.Encode(signature);

                Random Generator = new Random();
                var r = Generator.Next(0, Wallet.TotalPeers - 1);
                var peer = Wallet.Peers[r];
                var messageToSend = txtSendMessage.Text;
                var url = $"{peer}/api.php?q=send&version=1&public_key={Wallet.PublicKey}&signature={sig}&dst={recepient}&val={sum.ToString("N8")}&date={uTime.ToString()}&message={messageToSend}";
                var res = Wallet.get_json(url);
                if (string.IsNullOrEmpty(res)) {
                    MessageBox.Show(this, "Could not send the transaction to the peer! Please try again!", "", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return;
                }
                lblLastTransaction.Visible = txtLastTransaction.Visible = true;
                txtLastTransaction.Text = res;
            }
        }

        private void txtSendAmount_TextChanged(object sender, EventArgs e)
        {
            if (decimal.TryParse(txtSendAmount.Text.Trim(), out var amount)) {
                var fee = amount * Wallet.RATE;
                if (fee < Wallet.MINIMUM) {
                    fee = Wallet.MINIMUM;
                }
                lblFee.Text = $"{fee.ToString()} ARO";
            } else {
                lblFee.Text = "-";
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try {
                if (!syncWorker.IsBusy) {
                    syncWorker.RunWorkerAsync();
                }
            } catch {

            }
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            var saveFileDialog1 = new SaveFileDialog() {
                FileName = Wallet.WALLET_NAME,
                Filter = "ARO Wallet|*.aro"
            };
            var wallet = "arionum:" + Wallet.PrivateKey + ":" + Wallet.PublicKey;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                if (Wallet.IsEncrypted) {
                    wallet = Wallet.EncryptedWallet;
                }
                try {
                    using (var file = new StreamWriter(saveFileDialog1.FileName, false)) {
                        file.WriteLine(wallet);
                        file.Close();
                    }
                } catch {
                    MessageBox.Show(this, $"Could not write the wallet file. Please check the permissions on {saveFileDialog1.FileName}", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            var question = "Restoring another wallet will delete the current wallet. Are you sure you wish to proceed?";
            var response = MessageBox.Show(this, question, "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (response == DialogResult.Yes) {
                var openFileDialog1 = new OpenFileDialog() {
                    FileName = "",
                    Filter = "Aro Wallet (*.aro)|*.aro|All Files (*.*)|*.*"
                };
                if (openFileDialog1.ShowDialog() == DialogResult.OK) {

                    var uTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                    var wallet = $"arionum:{Wallet.PrivateKey}:{Wallet.PublicKey}";
                    if (Wallet.IsEncrypted) {
                        wallet = Wallet.EncryptedWallet;
                    }

                    // Save current wallet
                    try {
                        var backupPath = Path.Combine(Wallet.DataPath, $"wallet_backup_{uTime.ToString()}.aro");
                        using (var file = new StreamWriter(backupPath, false)) {
                            file.WriteLine(wallet);
                            file.Close();
                        }
                    } catch {
                        MessageBox.Show(this, "Could not write a backup the old wallet file. Restore failed.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Load wallet to restore
                    Wallet.IsEncrypted = false;
                    var s = "";
                    using (var reader = new StreamReader(openFileDialog1.FileName)) {
                        s = reader.ReadToEnd();
                        reader.Close();
                    }

                    // Check if encrypted
                    if (!s.StartsWith("arionum:")) {
                        Wallet.EncryptedWallet = s;
                        new frmDecrypt().ShowDialog();
                        s = Wallet.DecryptedWallet;
                        if (string.IsNullOrEmpty(s)) {
                            MessageBox.Show(this, "Could not decrypt wallet. Exiting...", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Environment.Exit(0);
                        }
                        Wallet.IsEncrypted = true;
                    }

                    var wal = s.Split(':');
                    Wallet.PrivateKey = wal[1];
                    Wallet.PublicKey = wal[2];

                    if (Wallet.IsEncrypted) {
                        wallet = Wallet.EncryptedWallet;
                    } else {
                        wallet = "arionum:" + Wallet.PrivateKey + ":" + Wallet.PublicKey;
                    }

                    try {
                        using (var file = new StreamWriter(Wallet.WalletPath, false)) {
                            file.WriteLine(wallet);
                            file.Close();
                        }
                    } catch {
                        var msg = $"Could not write the wallet file. Please check the permissions on {Wallet.WalletPath}. Restore failed.";
                        MessageBox.Show(this, msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    txtPublicKey.Text = Wallet.PublicKey = Wallet.PublicKey.Trim();
                    txtPrivateKey.Text = Wallet.PrivateKey = Wallet.PrivateKey.Trim();
                    if (Wallet.IsEncrypted) {
                        btnDecrypt.Text = "Decrypt";
                    } else {
                        btnDecrypt.Text = "Encrypt";
                    }

                    var encoder = new UTF8Encoding();
                    var sha512hasher = new SHA512Managed();
                    var enc = encoder.GetBytes(Wallet.PublicKey);
                    for (int i = 0; i <= 8; i++) {
                        enc = sha512hasher.ComputeHash(enc);
                    }
                    txtAddress.Text = Wallet.Address = SimpleBase.Base58.Bitcoin.Encode(enc);
                    if (!syncWorker.IsBusy) {
                        syncWorker.RunWorkerAsync();
                    }
                }
            }
        }

        private void btnDecrypt_Click(object sender, EventArgs e)
        {
            var wallet = "arionum:" + Wallet.PrivateKey + ":" + Wallet.PublicKey;
            if (Wallet.IsEncrypted) {
                var response = MessageBox.Show(this, "Are you sure you wish to decrypt the wallet?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (response == DialogResult.No) {
                    return;
                }
                Wallet.IsEncrypted = false;
            } else {
                new frmEncryption().ShowDialog();
                if (!Wallet.IsEncrypted) {
                    return;
                }
                if (Wallet.EncryptPass.Length < 8) {
                    MessageBox.Show(this, "Could not encrypt the wallet.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                wallet = Wallet.AES_Encrypt(wallet, Wallet.EncryptPass);
                Wallet.IsEncrypted = true;
            }

            try {
                using (var file = new StreamWriter(Wallet.WalletPath, false)) {
                    file.WriteLine(wallet);
                    file.Close();
                }
            } catch (Exception ex) {
                MessageBox.Show(this, ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                var msg = $"Could not write the wallet file. Please check the permissions on {Wallet.WalletPath}. Also, please save a backup of the current wallet in a different location.";
                MessageBox.Show(this, msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (Wallet.IsEncrypted) {
                    Wallet.IsEncrypted = false;
                } else {
                    Wallet.IsEncrypted = true;
                }
                return;
            }
            if (Wallet.IsEncrypted) {
                btnDecrypt.Text = "Decrypt";
            } else {
                btnDecrypt.Text = "Encrypt";
            }
        }

        private void txtSendAmount_KeyPress(object sender, KeyPressEventArgs e)
        {
            var isDigit = char.IsDigit(e.KeyChar);
            var isBackspace = (e.KeyChar == '\b');
            var isDecimalPoint = (e.KeyChar == '.') && (!txtSendAmount.Text.Contains('.'));
            var ignoreChar = !(isDigit || isBackspace || isDecimalPoint);
            e.Handled = ignoreChar;
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }
    }
}
