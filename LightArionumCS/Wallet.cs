using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LightArionumCS {
    static class Wallet {
        private static string private_key = "",
            public_key = "",
            encryptPass = "",
            encryptedWallet = "",
            decryptedWallet = "",
            address = "";

        public const decimal RATE = 0.0025m;
        public const decimal MINIMUM = 0.00000001m;
        public const string WALLET_NAME = "wallet.aro";

        public static string DataPath {
            get {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Arionum";
            }
        }

        public static string WalletPath {
            get {
                return Path.Combine(DataPath, WALLET_NAME);
            }
        }

        public static string PeersPath {
            get {
                return Path.Combine(DataPath, "peers.txt");
            }
        }

        public static int TotalPeers { get; set; }

        public static decimal Balance { get; set; }

        public static int SyncErr { get; set; }

        public static string[] Peers = new string[100];

        public static bool IsEncrypted { get; set; }

        public static string Address {
            get {
                if (address == null) {
                    address = "";
                }
                return address;
            }
            set {
                address = (value == null) ? "" : value;
            }
        }

        public static string PrivateKey {
            get {
                if (private_key == null) {
                    private_key = "";
                }
                return private_key;
            }
            set {
                private_key = (value == null) ? "" : value;
            }
        }

        public static string PublicKey {
            get {
                if (public_key == null) {
                    public_key = "";
                }
                return public_key;
            }
            set {
                public_key = (value == null) ? "" : value;
            }
        }

        public static string EncryptPass {
            get {
                if (encryptPass == null) {
                    encryptPass = "";
                }
                return encryptPass;
            }
            set {
                encryptPass = (value == null) ? "" : value;
            }
        }

        public static string EncryptedWallet {
            get {
                if (encryptedWallet == null) {
                    encryptedWallet = "";
                }
                return encryptedWallet;
            }
            set {
                encryptedWallet = (value == null) ? "" : value;
            }
        }

        public static string DecryptedWallet {
            get {
                if (decryptedWallet == null) {
                    decryptedWallet = "";
                }
                return decryptedWallet;
            }
            set {
                decryptedWallet = (value == null) ? "" : value;
            }
        }

        public static string get_json(string url)
        {
            var result = "";
            try {
                var rawresp = http_get(url);
                if (!string.IsNullOrEmpty(rawresp)) {
                    var array = JObject.Parse(rawresp);
                    if (array.GetValue("status").ToString().Equals("ok")) {
                        result = array.GetValue("data").ToString();
                    }
                }
            } catch {
                result = "";
            }
            return result;
        }

        public static string http_get(string url)
        {
            // Console.WriteLine(url)
            var rawresp = "";
            try {
                var request = WebRequest.Create(url) as HttpWebRequest;
                var response = request.GetResponse() as HttpWebResponse;

                using (var reader = new StreamReader(response.GetResponseStream())) {
                    rawresp = reader.ReadToEnd();
                    reader.Close();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                rawresp = "";
            }
            return rawresp.Trim();
        }

        // Decrypt a string with AES
        public static string AES_Decrypt(string input, string pass)
        {
            var AES = new RijndaelManaged();
            var Hash_AES = new SHA256Managed();
            input = input.Trim();
            var iv = new byte[16];
            try {
                var tmp = Convert.FromBase64String(input);
                Array.Copy(tmp, 0, iv, 0, 16);
                var tmp2 = new byte[tmp.Length - 16];
                Array.Copy(tmp, 16, tmp2, 0, tmp.Length - 16);
                input = Encoding.ASCII.GetString(tmp2);
                var hash = Hash_AES.ComputeHash(Encoding.ASCII.GetBytes(pass));
                AES.Key = hash;
                AES.Mode = CipherMode.CBC;
                AES.BlockSize = 128;
                AES.IV = iv;
                var DESDecrypter = AES.CreateDecryptor();

                var Buffer = Convert.FromBase64String(input);
                var decrypted = Encoding.ASCII.GetString(DESDecrypter.TransformFinalBlock(Buffer, 0, Buffer.Length));
                return decrypted;
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                return input; ; // If decryption fails, return the unaltered input.
            }
        }

        public static string AES_Encrypt(string input, string pass)
        {
            var AES = new RijndaelManaged();
            var Hash_AES = new SHA256Managed();
            var encrypted = "";
            try {
                var hash = Hash_AES.ComputeHash(Encoding.ASCII.GetBytes(pass));
                AES.Key = hash;
                AES.Mode = CipherMode.CBC;
                AES.BlockSize = 128;
                AES.GenerateIV();

                var iv = AES.IV;
                var DESEncrypter = AES.CreateEncryptor();
                var Buffer = Encoding.ASCII.GetBytes(input);
                encrypted = Convert.ToBase64String(DESEncrypter.TransformFinalBlock(Buffer, 0, Buffer.Length));
                var tmp = Encoding.ASCII.GetBytes(encrypted);
                var tmp2 = new byte[tmp.Length + 16];
                Array.Copy(iv, tmp2, 16);
                Array.Copy(tmp, 0, tmp2, 16, tmp.Length);

                return Convert.ToBase64String(tmp2);
            } catch {
                return input; // If encryption fails, return the unaltered input.
            }
        }

        public static string coin2pem(string data, bool is_private = false)
        {
            var enc = SimpleBase.Base58.Bitcoin.Decode(data);
            var tmp = Convert.ToBase64String(enc);
            var tmp2 = "";
            for (int i = 0; i < tmp.Length; i++) {
                tmp2 = tmp2 + tmp[i];
                if (((i % 64) == 0) && (i > 0)) {
                    tmp2 = tmp2 + "\r\n";
                }
            }

            var final = $"-----BEGIN PUBLIC KEY-----\r\n{tmp2}\r\n-----END PUBLIC KEY-----";
            if (is_private) {
                final = $"-----BEGIN EC PRIVATE KEY-----\r\n{tmp2}\r\n-----END EC PRIVATE KEY-----";
            }
            return final;
        }

        private static string pem2coin(String data)
        {
            data = data.Replace("-----BEGIN PUBLIC KEY-----", "");
            data = data.Replace("-----END PUBLIC KEY-----", "");
            data = data.Replace("-----BEGIN EC PRIVATE KEY-----", "");
            data = data.Replace("-----END EC PRIVATE KEY-----", "");
            data = data.Replace("-----BEGIN PRIVATE KEY-----", "");
            data = data.Replace("-----END PRIVATE KEY-----", "");
            data = data.Replace("-----END PRIVATE KEY-----", "");
            data = data.Replace("\r\n", "");
            data = data.Replace("\r", "");
            data = data.Replace("\n", "");
            byte[] enc = Convert.FromBase64String(data);
            return SimpleBase.Base58.Bitcoin.Encode(enc);
        }

        public static void generate_keys()
        {
            X9ECParameters EC = SecNamedCurves.GetByName("secp256k1");
            ECDomainParameters domainParams = new ECDomainParameters(EC.Curve, EC.G, EC.N, EC.H);
            SecureRandom Random = new SecureRandom();

            // Generate EC KeyPair
            ECKeyPairGenerator keyGen = new ECKeyPairGenerator();
            ECKeyGenerationParameters keyParams = new ECKeyGenerationParameters(domainParams, Random);
            keyGen.Init(keyParams);
            AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();

            ECPrivateKeyParameters privateKeyParams = (ECPrivateKeyParameters)keyPair.Private;
            ECPublicKeyParameters publicKeyParams = (ECPublicKeyParameters)keyPair.Public;

            // Get Private Key
            BigInteger privD = privateKeyParams.D;
            byte[] privBytes = privD.ToByteArray();

            byte[] temp = new byte[32];
            if (privBytes.Length == 33) {
                Array.Copy(privBytes, 1, temp, 0, 32);
                privBytes = temp;
            } else if (privBytes.Length < 32) {
                temp = Enumerable.Repeat<byte>(0, 32).ToArray();
                Array.Copy(privBytes, 0, temp, 32 - privBytes.Length, privBytes.Length);
                privBytes = temp;
            }
            string privKey = BitConverter.ToString(privBytes).Replace("-", "");

            // Get Compressed Public Key
            Org.BouncyCastle.Math.EC.ECPoint q = publicKeyParams.Q;
            FpPoint fp = new FpPoint(EC.Curve, q.AffineXCoord, q.AffineYCoord);
            byte[] enc = fp.GetEncoded(true);
            string compressedPubKey = BitConverter.ToString(enc).Replace("-", "");

            // Get Uncompressed Public Key
            enc = fp.GetEncoded(false);
            string uncompressedPubKey = BitConverter.ToString(enc).Replace("-", "");
            byte[] pubk = SimpleBase.Base16.Decode(compressedPubKey);
            public_key = SimpleBase.Base58.Bitcoin.Encode(pubk);
            byte[] pvkas = SimpleBase.Base16.Decode(privKey);
            private_key = SimpleBase.Base58.Bitcoin.Encode(pvkas);

            // Output
            StringWriter textWriter = new StringWriter();
            PemWriter pemWriter = new PemWriter(textWriter);

            pemWriter.WriteObject(keyPair.Private);
            pemWriter.Writer.Flush();
            string tmp_private = textWriter.ToString();
            // Console.WriteLine(pem2coin(textWriter.ToString()));
            private_key = pem2coin(textWriter.ToString());
            textWriter = new StringWriter();
            pemWriter = new PemWriter(textWriter);
            pemWriter.WriteObject(keyPair.Public);
            pemWriter.Writer.Flush();

            Chilkat.PublicKey a = new Chilkat.PublicKey();
            a.LoadFromString(textWriter.ToString());
            public_key = a.GetEncoded(true, "base58");
        }
    }
}
