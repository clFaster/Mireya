# Mireya Digital Signage - Frontend

## Getting Started

### Prerequisites
- Node.js 20+ and npm/yarn/pnpm/bun

### Installation

1. Install dependencies:
```bash
npm install
# or
yarn install
# or
pnpm install
# or
bun install
```

2. Create environment file:
```bash
cp .env.local.example .env.local
```

3. Update the API URL in `.env.local` if needed:
```
NEXT_PUBLIC_API_URL=http://localhost:5000
```

### Running the Development Server

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser.

## Features

### Authentication
- **Login Page** (`/`) - Main login interface
- **Dashboard** (`/dashboard`) - Protected route showing user information
- **Admin Password Reset** (`/reset-admin-password`) - Emergency admin password recovery

### API Integration
The frontend communicates with the ASP.NET Core backend using the centralized API client located in `src/lib/api.ts`.

#### Available API Methods:
- `apiClient.login(email, password)` - User authentication
- `apiClient.getUserInfo(token)` - Fetch current user information
- `apiClient.refreshToken(refreshToken)` - Refresh access token
- `apiClient.resetAdminPassword(resetToken, newPassword)` - Reset admin password

### Token Management
Access tokens and refresh tokens are stored in `localStorage`:
- `accessToken` - Used for API authentication
- `refreshToken` - Used to obtain new access tokens

## Project Structure

```
src/
├── app/
│   ├── dashboard/
│   │   └── page.tsx          # Protected dashboard page
│   ├── reset-admin-password/
│   │   └── page.tsx          # Admin password reset
│   ├── layout.tsx            # Root layout
│   ├── page.tsx              # Login page (home)
│   └── globals.css           # Global styles
└── lib/
    └── api.ts                # API client utilities
```

## Default Admin Credentials

On first run, the backend creates a default admin user:
- **Email**: admin@mireya.local
- **Password**: Admin123!

⚠️ **Change these credentials immediately after first login!**

## Environment Variables

- `NEXT_PUBLIC_API_URL` - Backend API base URL (default: http://localhost:5000)

## Learn More

To learn more about Next.js:
- [Next.js Documentation](https://nextjs.org/docs) - Learn about Next.js features and API
- [Learn Next.js](https://nextjs.org/learn) - Interactive Next.js tutorial

## Deployment

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new).

Check out the [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
