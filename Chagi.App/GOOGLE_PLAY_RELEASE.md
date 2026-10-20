# Google Play Release

This project is configured to produce an Android App Bundle (`.aab`) for release builds, which is the format Google Play expects for new app uploads.

## Before first upload

1. Confirm the permanent package name.
   The project currently uses `se.kinnevalds.chagi` in [ITF-Taekwondo-Gradering.csproj](D:/work/tkd-appen/ITF-Taekwondo-Gradering/ITF-Taekwondo-Gradering.csproj).
   Google Play package names are unique and permanent, so the Play Console app must be created with this exact identifier unless you intentionally change it before first upload.
2. Make sure `ApplicationVersion` is increased for every new upload.
3. Keep the keystore and password backed up securely.

## Build a signed AAB

Create a text file that contains only the keystore password, for example `play-signing\\android-signing-password.txt`.

Example:

```powershell
.\Publish-GooglePlay.ps1 `
  -KeystorePath .\play-signing\chagi-upload.jks `
  -KeyAlias chagi-upload `
  -PasswordFile .\play-signing\android-signing-password.txt `
  -ApplicationId se.kinnevalds.chagi `
  -ApplicationVersion 8 `
  -ApplicationDisplayVersion 2.1
```

The signed bundle will be written to:

```text
bin\Release\net9.0-android\publish
```

## Play Console flow

1. Create the app in Play Console.
2. Use the same package name as the bundle `ApplicationId`.
3. Enroll in Play App Signing.
   For new apps, Google can generate the app signing key and you keep using your local keystore as the upload key.
4. Upload the signed `.aab` to an internal test track first.
5. Complete store listing, app content, privacy policy, and Data safety.
6. Roll forward to closed testing or production after the internal build is accepted.

## References

- .NET MAUI Android publishing: https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0
- Play Console app setup: https://support.google.com/googleplay/android-developer/answer/9859152?hl=en
- Play App Signing: https://support.google.com/googleplay/android-developer/answer/9842756?hl=en
