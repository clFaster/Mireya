import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Mireya Digital Signage",
  description: "Digital Signage Management System",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">
        {children}
      </body>
    </html>
  );
}
