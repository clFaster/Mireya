"use client";

import { createContext, useContext, type ReactNode } from "react";
import { Client } from "./generated/client";

// Get API base URL from environment variable or default to localhost
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

// Create fetch implementation with logging and credentials
const fetchImplementation = {
  fetch: (url: RequestInfo, init?: RequestInit): Promise<Response> => {
    console.log("Making API request to:", url);
    // Always include credentials (cookies) in requests
    return fetch(url, { ...init, credentials: "include" });
  },
};

// Create and configure the API client with middleware
const apiClient = new Client(API_BASE_URL, fetchImplementation).withMiddleware({
  onRequest: async (options) => {
    // Add auth headers, logging, etc.
    console.log("API Request:", options);

    // Ensure credentials are included for cookie-based authentication
    options.credentials = "include";

    return options;
  },
  onResponse: async (response) => {
    // Handle errors, logging, etc.
    console.log("API Response:", response.status, response.url);

    if (!response.ok) {
      console.error("API Error:", response.status, response.statusText);

      // Handle 401 Unauthorized - redirect to login
      if (response.status === 401 && typeof window !== "undefined") {
        // With cookie authentication, just redirect to login
        // The browser will handle cookie cleanup
        window.location.href = "/";
      }
    }

    return response;
  },
});

// Create React context for the API client
const ApiContext = createContext<Client | null>(null);

interface ApiProviderProps {
  children: ReactNode;
}

/**
 * Provider component that makes the API client available to all child components
 * Wrap your app with this provider to use the useApi hook
 */
export const ApiProvider = ({ children }: ApiProviderProps) => {
  return (
    <ApiContext.Provider value={apiClient}>{children}</ApiContext.Provider>
  );
};

/**
 * Hook to access the API client in React components
 * Must be used within an ApiProvider
 *
 * @example
 * ```tsx
 * const api = useApi();
 * const response = await api.postLogin({ email, password });
 * ```
 */
export const useApi = (): Client => {
  const context = useContext(ApiContext);
  if (!context) {
    throw new Error("useApi must be used within an ApiProvider");
  }
  return context;
};

// Re-export the Client class and types from generated client for convenience
export { Client } from "./generated/client";

export type {
  RegisterRequest,
  LoginRequest,
  AccessTokenResponse,
  RefreshRequest,
  InfoResponse,
  InfoRequest,
  TwoFactorRequest,
  TwoFactorResponse,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  ResendConfirmationEmailRequest,
  SwaggerResponse,
  ApiException,
} from "./generated/client";
