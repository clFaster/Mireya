"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useApi } from "@/lib/api";
import type { InfoResponse } from "@/lib/api";

export default function Dashboard() {
  const [userInfo, setUserInfo] = useState<InfoResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();
  const api = useApi();

  useEffect(() => {
    const fetchUserInfo = async () => {
      const token = localStorage.getItem("accessToken");
      
      if (!token) {
        router.push("/");
        return;
      }

      try {
        const response = await api.getManageInfo();
        if (response.result) {
          setUserInfo(response.result);
        }
      } catch (error) {
        console.error("Error fetching user info:", error);
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");
        router.push("/");
      } finally {
        setIsLoading(false);
      }
    };

    fetchUserInfo();
  }, [router, api]);

  const handleLogout = () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    router.push("/");
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <div className="text-gray-900 dark:text-white">Loading...</div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-white dark:bg-gray-800 shadow rounded-lg">
          <div className="px-4 py-5 sm:p-6">
            <div className="flex justify-between items-center mb-6">
              <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
                Dashboard
              </h1>
              <button
                onClick={handleLogout}
                className="px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500"
              >
                Logout
              </button>
            </div>
            
            {userInfo && (
              <div className="space-y-4">
                <div>
                  <h2 className="text-lg font-medium text-gray-900 dark:text-white mb-2">
                    User Information
                  </h2>
                  <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <div>
                      <dt className="text-sm font-medium text-gray-500 dark:text-gray-400">
                        Email
                      </dt>
                      <dd className="mt-1 text-sm text-gray-900 dark:text-white">
                        {userInfo.email}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-sm font-medium text-gray-500 dark:text-gray-400">
                        Email Confirmed
                      </dt>
                      <dd className="mt-1 text-sm text-gray-900 dark:text-white">
                        {userInfo.isEmailConfirmed ? "Yes" : "No"}
                      </dd>
                    </div>
                  </dl>
                </div>
                
                <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                  <p className="text-sm text-gray-600 dark:text-gray-400">
                    Welcome to Mireya Digital Signage! You are successfully authenticated.
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
