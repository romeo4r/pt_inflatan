import type { Metadata } from "next";
import Link from "next/link";
import { Geist } from "next/font/google";
import "./globals.css";

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Mundialito Corporativo",
  description: "Gestión del torneo de fútbol corporativo",
};

const NAV = [
  { href: "/standings", label: "Tabla de posiciones" },
  { href: "/top-scorers", label: "Goleadores" },
  { href: "/teams", label: "Equipos" },
  { href: "/matches", label: "Partidos" },
] as const;

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="es" className={`${geistSans.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col">
        <header className="border-b border-gray-300 bg-gray-50">
          <nav className="mx-auto flex max-w-5xl flex-wrap items-center gap-4 px-4 py-3">
            <Link href="/" className="font-semibold">
              Mundialito
            </Link>
            {NAV.map((item) => (
              <Link key={item.href} href={item.href} className="text-sm text-blue-800 hover:underline">
                {item.label}
              </Link>
            ))}
          </nav>
        </header>
        <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">{children}</main>
        <footer className="border-t border-gray-200 px-4 py-3 text-center text-xs text-gray-500">
          Prueba técnica · .NET 10 + Next.js
        </footer>
      </body>
    </html>
  );
}
