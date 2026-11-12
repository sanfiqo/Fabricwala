# TextileConnect secrets

Store local development values with `dotnet user-secrets` for the API project. Use environment variables or the deployment secret store outside development. Do not put values in `appsettings.json`, `appsettings.Development.json`, source code, or git.

Required and reserved secret names:

- `ConnectionStrings:TextileConnect`
- `UserContacts:LookupHmacKey`
- `Otp:StorageHmacKey`
- `DataProtection:KeyRingConnectionString`
- `Otp:Msg91:AuthKey`
- `Otp:Msg91:TemplateId`
- `WhatsApp:AccessToken`
- `WhatsApp:PhoneNumberId`
- `Payments:Razorpay:KeyId`
- `Payments:Razorpay:KeySecret`
- `Payments:Razorpay:WebhookSecret`
- `Storage:R2:AccountId`
- `Storage:R2:AccessKeyId`
- `Storage:R2:SecretAccessKey`
- `Storage:R2:BucketName`

Example command shape (replace the placeholder locally; never commit the value):

```powershell
dotnet user-secrets set "ConnectionStrings:TextileConnect" "<local-value>" --project src/TextileConnect.Api
```
