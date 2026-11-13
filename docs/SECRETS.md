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

## Production requirements

Configure these values through the deployment secret store or environment variables; do not add them to an appsettings file:

- `ConnectionStrings:TextileConnect` must point to the production MySQL database.
- `UserContacts:LookupHmacKey` and `Otp:StorageHmacKey` must be independent, random production secrets of at least 32 bytes. Never reuse the fixed local Development values.
- `DataProtection:KeyRingConnectionString` must use a durable, access-controlled production key ring.
- Configure the external OTP, WhatsApp, payment, and object-storage credentials listed above when those integrations are enabled.

The API refuses to start in `Production` if either HMAC setting matches a known local Development value. This check does not replace secret storage, rotation, or deployment validation.
