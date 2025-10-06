# Mireya Authentication Guide

## Overview
The application uses **ASP.NET Core Identity** with the built-in `MapIdentityApi<User>()` endpoints for authentication. This provides a complete, secure authentication system out of the box.

## Available Endpoints

### Identity API Endpoints (Automatically Generated)
All endpoints are prefixed with `/` and use bearer token authentication:

- **POST /register** - Register a new user
  ```json
  {
    "email": "user@example.com",
    "password": "SecurePassword123!"
  }
  ```

- **POST /login?useCookies=false&useSessionCookies=false** - Login
  ```json
  {
    "email": "user@example.com",
    "password": "SecurePassword123!"
  }
  ```
  Returns an access token and refresh token.

- **POST /refresh** - Refresh access token
  ```json
  {
    "refreshToken": "your-refresh-token"
  }
  ```

- **POST /forgotPassword** - Request password reset
  ```json
  {
    "email": "user@example.com"
  }
  ```

- **POST /resetPassword** - Reset password with token
  ```json
  {
    "email": "user@example.com",
    "resetCode": "code-from-email",
    "newPassword": "NewSecurePassword123!"
  }
  ```

- **GET /manage/info** - Get current user info (requires authentication)
- **POST /manage/info** - Update user info (requires authentication)

### Custom Endpoints

- **POST /api/auth/reset-admin-password** - Emergency admin password reset
  ```json
  {
    "resetToken": "configured-reset-token",
    "newPassword": "NewAdminPassword123!"
  }
  ```
  This endpoint allows resetting the admin password using a special reset token configured in `appsettings` or user secrets.

## Password Requirements
- Minimum 8 characters
- Requires uppercase letter
- Requires lowercase letter
- Requires digit
- Requires non-alphanumeric character

## Default Admin User
On first startup, the application creates a default admin user with credentials from configuration:
- **Username**: admin (from `DefaultAdminUser:Username`)
- **Email**: admin@mireya.local (from `DefaultAdminUser:Email`)
- **Password**: Set in `appsettings.Development.json` or user secrets (default: Admin123!)

⚠️ **Important**: Change the default admin password immediately after first login!

## Admin Password Reset Options

### Option 1: Use the Web Interface (Recommended)
The application includes a dedicated admin password reset page at `/reset-admin-password`:

1. Navigate to the reset page (link available on login page)
2. Enter the reset token configured in your backend (from `AdminPasswordResetToken` setting)
3. Enter and confirm the new password (must meet password requirements)
4. Submit the form

The reset token should be:
- Configured in `appsettings.Development.json` for development
- Stored in user secrets or environment variables for production
- Kept highly secure and only shared with authorized personnel

**Configuration** (in `appsettings.Development.json` or user secrets):
```json
{
  "AdminPasswordResetToken": "your-secret-reset-token"
}
```

### Option 2: Use the API Endpoint Directly
If you lose access to the admin account, you can reset it using the API endpoint with curl or any HTTP client:

```bash
curl -X POST http://localhost:5000/api/auth/reset-admin-password \
  -H "Content-Type: application/json" \
  -d '{
    "resetToken": "your-secret-reset-token",
    "newPassword": "NewPassword123!"
  }'
```

### Option 3: Delete the Database (Development Only)
In development with SQLite, you can simply delete `mireya.db` and restart the application. It will recreate the database with migrations and the default admin user.

**Steps:**
1. Stop the backend application
2. Delete `src/Mireya.Api/mireya.db`
3. Start the backend application
4. The default admin user will be recreated with the configured password

### Option 4: Direct Database Access
For production environments, you can:
1. Connect to your database directly (e.g., using pgAdmin for PostgreSQL)
2. Delete the admin user record from the `AspNetUsers` table
3. Remove associated records from `AspNetUserRoles` table
4. Restart the application - it will recreate the default admin user on startup

Example SQL for PostgreSQL:
```sql
DELETE FROM "AspNetUserRoles" WHERE "UserId" IN (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'admin@mireya.local');
DELETE FROM "AspNetUsers" WHERE "Email" = 'admin@mireya.local';
```

### Option 5: Use User Secrets for Development
Set a secure reset token using user secrets:

```bash
cd src/Mireya.Api
dotnet user-secrets set "AdminPasswordResetToken" "your-very-secure-random-token"
dotnet user-secrets set "DefaultAdminUser:Password" "YourSecurePassword123!"
```

## Security Considerations

1. **Change Default Credentials**: Always change the default admin password after first login
2. **Secure Reset Token**: In production, use a strong, random `AdminPasswordResetToken` stored in user secrets or environment variables
3. **HTTPS Only**: Always use HTTPS in production
4. **Token Storage**: Store access tokens securely:
   - Frontend: httpOnly cookies or secure storage
   - Mobile apps: Secure storage APIs (Keychain/Keystore)
5. **Password Policy**: The default password policy is enforced for all users including admins
6. **Account Lockout**: Failed login attempts trigger account lockout (5 attempts, 5-minute lockout)
7. **Token Expiration**: Access tokens expire after 1 hour (configurable)
8. **Refresh Tokens**: Use refresh tokens to obtain new access tokens without re-authentication

## Configuration via User Secrets (Recommended for Development)

```bash
cd src/Mireya.Api

# Set default admin password
dotnet user-secrets set "DefaultAdminUser:Username" "admin"
dotnet user-secrets set "DefaultAdminUser:Email" "admin@mireya.local"
dotnet user-secrets set "DefaultAdminUser:Password" "YourSecurePassword123!"

# Set emergency reset token
dotnet user-secrets set "AdminPasswordResetToken" "your-secret-reset-token"
```

## Frontend Integration

The frontend uses a centralized API client (`src/lib/api.ts`) to communicate with the backend:

### Login Flow
1. User enters email and password on login page
2. Frontend calls `apiClient.login(email, password)`
3. Backend validates credentials and returns access token + refresh token
4. Frontend stores tokens in `localStorage`
5. User is redirected to dashboard

### Authenticated Requests
1. Frontend retrieves access token from `localStorage`
2. Includes token in Authorization header: `Bearer {accessToken}`
3. Backend validates token and processes request

### Token Refresh
1. When access token expires, frontend receives 401 Unauthorized
2. Frontend calls `apiClient.refreshToken(refreshToken)`
3. Backend validates refresh token and returns new access token
4. Frontend stores new access token and retries original request

### Pages
- `/` - Login page
- `/dashboard` - Protected dashboard (requires authentication)
- `/reset-admin-password` - Admin password reset (requires reset token)

## Troubleshooting

### "Invalid email or password" error
- Verify the credentials match the configured default admin user
- Check the database to ensure the user exists
- Review application logs for detailed error messages

### "Invalid reset token" error
- Verify the reset token matches the configured `AdminPasswordResetToken`
- Ensure the token is set in the correct configuration source (appsettings or user secrets)

### Token expired errors
- Use the refresh token to obtain a new access token
- If refresh token is also expired, re-authenticate

### CORS errors
- Ensure the frontend URL is allowed in the CORS policy (configured in `Program.cs`)
- Check browser console for specific CORS error details
