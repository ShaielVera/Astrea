import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono, Lora } from "next/font/google";
import { Encabezado } from "@/components/layout/Encabezado";
import { ListasDesplegables } from "@/components/ui/ListasDesplegables";
import { AuthProvider } from "@/features/auth/AuthProvider";
import { RegistrarServiceWorker } from "@/features/pwa/RegistrarServiceWorker";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

// Serif de los títulos, en la línea institucional de las demás apps del instituto.
const lora = Lora({
  variable: "--font-lora",
  subsets: ["latin"],
  weight: ["500", "600", "700"],
});

export const metadata: Metadata = {
  title: "Astrea",
  description: "Portal académico e institucional del Instituto Superior de Profesorado N.º 20.",
  applicationName: "Astrea",
  appleWebApp: { capable: true, title: "Astrea", statusBarStyle: "default" },
  icons: { apple: "/icons/apple-touch-icon.png" },
};

export const viewport: Viewport = {
  themeColor: "#17365a",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html
      lang="es"
      className={`${geistSans.variable} ${geistMono.variable} ${lora.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <a
          href="#contenido"
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-indigo-600 focus:px-4 focus:py-2 focus:text-white"
        >
          Saltar al contenido
        </a>
        <AuthProvider>
          <Encabezado />
          <div id="contenido" tabIndex={-1} className="flex flex-1 flex-col outline-none">
            {children}
          </div>
        </AuthProvider>
        <RegistrarServiceWorker />
        <ListasDesplegables />
      </body>
    </html>
  );
}
