# Mireya API Client - Quick Start Guide

This guide will help you get started with the auto-generated, type-safe API client for Mireya Digital Signage.

## Overview

The project uses **NSwag** to automatically generate a TypeScript client from your ASP.NET Core API. This provides:

- ✅ **Full type safety** - TypeScript interfaces for all requests/responses
- ✅ **Automatic synchronization** - Regenerate when API changes
- ✅ **Middleware support** - Add auth tokens, logging, error handling
- ✅ **React Context integration** - Easy to use in Next.js components

## Setup Your App

### 1. Wrap your app with the API Provider

In your root layout (`app/layout.tsx` or `_app.tsx`):

```tsx
import { ApiProvider } from "@/lib/api";

export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <ApiProvider>{children}</ApiProvider>
      </body>
    </html>
  );
}
```

### 2. Configure the API URL

Create `.env.local` file (copy from `.env.example`):

```bash
NEXT_PUBLIC_API_URL=http://localhost:5000
```

## Using the API Client

### Basic Usage

```tsx
"use client";

import { useApi } from "@/lib/api";
import type { LoginRequest } from "@/lib/api";

export function LoginForm() {
  const api = useApi();

  const handleLogin = async (email: string, password: string) => {
    try {
      const request: LoginRequest = { email, password };
      const response = await api.postLogin(true, false, request);

      if (response.result?.accessToken) {
        // Store token
        localStorage.setItem("token", response.result.accessToken);
        console.log("Login successful!");
      }
    } catch (error) {
      console.error("Login failed:", error);
    }
  };

  // Your form JSX here
}
```

### Available API Methods

The generated `Client` class includes all your API endpoints:

#### Authentication

- `postRegister(request)` - Register a new user
- `postLogin(useCookies, useSessionCookies, request)` - Login
- `postRefresh(request)` - Refresh access token
- `postForgotPassword(request)` - Request password reset
- `postResetPassword(request)` - Reset password with code

#### User Management

- `getManageInfo()` - Get current user info
- `postManageInfo(request)` - Update user info
- `postManage2fa(request)` - Manage 2FA settings
- `getConfirmEmail(userId, code, changedEmail)` - Confirm email
- `postResendConfirmationEmail(request)` - Resend confirmation

### Type-Safe Requests

All request/response types are available:

```tsx
import type {
  LoginRequest,
  RegisterRequest,
  AccessTokenResponse,
  InfoResponse,
  // ... all other types
} from "@/lib/api";
```

## Advanced Usage

### Custom Authentication Hook

```tsx
import { useApi } from "@/lib/api";
import { useState } from "react";

export function useAuth() {
  const api = useApi();
  const [user, setUser] = useState(null);

  const login = async (email: string, password: string) => {
    const response = await api.postLogin(true, false, { email, password });
    if (response.result?.accessToken) {
      localStorage.setItem("token", response.result.accessToken);
      // Fetch user info
      const userInfo = await api.getManageInfo();
      setUser(userInfo.result);
    }
    return response.result;
  };

  const logout = () => {
    localStorage.removeItem("token");
    setUser(null);
  };

  return { user, login, logout };
}
```

### Adding Authentication to Requests

Edit `src/lib/api/index.tsx` to add auth headers automatically:

```tsx
const apiClient = new Client(API_BASE_URL, fetchImplementation).withMiddleware({
  onRequest: async (options) => {
    // Add auth token from storage
    const token = localStorage.getItem("token");
    if (token && options.headers) {
      (options.headers as any)["Authorization"] = `Bearer ${token}`;
    }
    return options;
  },
  onResponse: async (response) => {
    // Handle 401 Unauthorized
    if (response.status === 401) {
      localStorage.removeItem("token");
      window.location.href = "/login";
    }
    return response;
  },
});
```

## Regenerating the Client

When you make changes to your API, regenerate the TypeScript client:

```bash
npm run generate:api
```

Or with dotnet directly:

```bash
dotnet nswag run nswag.json
```

## Error Handling

All API methods return `Promise<SwaggerResponse<T>>`. Handle errors with try-catch:

```tsx
try {
  const response = await api.postRegister({ email, password });
  console.log("Success:", response.status);
} catch (error) {
  if (error instanceof ApiException) {
    console.error("API Error:", error.status, error.message);
    console.error("Details:", error.result);
  }
}
```

## Response Structure

All responses follow this structure:

```tsx
interface SwaggerResponse<T> {
  status: number; // HTTP status code
  headers: { [key: string]: any };
  result: T; // The actual response data
}
```

## Files Structure

```
src/lib/api/
├── index.tsx              # Main API setup, exports useApi hook
├── generated/
│   └── client.ts          # Auto-generated API client (don't edit!)
├── examples.tsx           # Usage examples
└── README.md              # This file
```

## Best Practices

1. **Never edit generated files** - They will be overwritten on regeneration
2. **Use the `useApi` hook** - Don't create Client instances manually
3. **Type your requests** - Use the exported TypeScript interfaces
4. **Handle errors** - Always wrap API calls in try-catch
5. **Store tokens securely** - Consider using httpOnly cookies in production
6. **Regenerate after API changes** - Keep frontend in sync with backend

## Troubleshooting

### "Module has no exported member"

- Run `npm run generate:api` to regenerate the client
- Restart your TypeScript server in VS Code

### "useApi must be used within an ApiProvider"

- Make sure `<ApiProvider>` wraps your component tree

### API calls fail with CORS errors

- Check that your API has CORS configured for your Next.js dev server
- Verify `NEXT_PUBLIC_API_URL` is set correctly

## Example Project Setup

See `src/lib/api/examples.tsx` for complete working examples of:

- User registration
- Login/logout
- Fetching user info
- Custom authentication hooks

## Need More Help?

- See `API_GENERATION.md` for technical details on NSwag configuration
- Check the generated `client.ts` for all available methods
- Review your API's Swagger UI at `http://localhost:5000/scalar/v1`
