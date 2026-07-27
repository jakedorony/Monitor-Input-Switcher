// AuthStore.cs - persists the Supabase session (email + refresh token) at
// %APPDATA%\MonitorSwitch\auth.dat, encrypted with DPAPI (current user scope)
// so it can only be decrypted by this Windows account on this machine.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MonitorSwitch
{
    static class AuthStore
    {
        class AuthDto
        {
            public string Email { get; set; }
            public string RefreshToken { get; set; }
        }

        static string FilePath { get { return Path.Combine(ConfigStore.Dir, "auth.dat"); } }

        public static void Save(string email, string refreshToken)
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.Dir);
                var dto = new AuthDto { Email = email, RefreshToken = refreshToken };
                byte[] plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto));
                File.WriteAllBytes(FilePath, Dpapi(plain, true));
            }
            catch
            {
                // Not fatal: the user just has to sign in again next launch.
            }
        }

        public static bool TryLoad(out string email, out string refreshToken)
        {
            email = null;
            refreshToken = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                byte[] plain = Dpapi(File.ReadAllBytes(FilePath), false);
                var dto = JsonSerializer.Deserialize<AuthDto>(Encoding.UTF8.GetString(plain));
                if (dto == null || string.IsNullOrEmpty(dto.RefreshToken)) return false;
                email = dto.Email;
                refreshToken = dto.RefreshToken;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch { }
        }

        static byte[] Dpapi(byte[] input, bool protect)
        {
            var inBlob = new Native.DATA_BLOB();
            var outBlob = new Native.DATA_BLOB();
            inBlob.cbData = input.Length;
            inBlob.pbData = Marshal.AllocHGlobal(input.Length);
            try
            {
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);
                bool ok = protect
                    ? Native.CryptProtectData(ref inBlob, "MonitorSwitch", IntPtr.Zero,
                        IntPtr.Zero, IntPtr.Zero, Native.CRYPTPROTECT_UI_FORBIDDEN, ref outBlob)
                    : Native.CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero,
                        IntPtr.Zero, IntPtr.Zero, Native.CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);
                if (!ok) throw new InvalidOperationException("DPAPI call failed");

                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) Native.LocalFree(outBlob.pbData);
            }
        }
    }
}
