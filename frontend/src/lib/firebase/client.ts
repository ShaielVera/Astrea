import { getApp, getApps, initializeApp, type FirebaseOptions } from "firebase/app";
import { connectAuthEmulator, getAuth, type Auth } from "firebase/auth";

// La configuración web de Firebase es pública por diseño: identifica el proyecto, no da permisos.
// La seguridad la aplica el backend validando cada ID Token.
const firebaseConfig: FirebaseOptions = {
  apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY,
  authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
  projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
  storageBucket: process.env.NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID,
  appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID,
};

const emuladorAuth = process.env.NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_URL;

let auth: Auth | undefined;

/** Devuelve la instancia de Firebase Auth (solo en el navegador). */
export function obtenerAuth(): Auth {
  if (auth) return auth;

  const app = getApps().length > 0 ? getApp() : initializeApp(firebaseConfig);
  auth = getAuth(app);

  if (emuladorAuth) {
    connectAuthEmulator(auth, emuladorAuth, { disableWarnings: true });
  }

  return auth;
}
