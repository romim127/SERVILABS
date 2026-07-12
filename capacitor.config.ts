import type { CapacitorConfig } from '@capacitor/cli';

const publicUrl = process.env.APP_PUBLIC_URL?.trim() || 'https://appservicios-mn6i.onrender.com';

const config: CapacitorConfig = {
  appId: 'com.appservicios.app',
  appName: 'SERVILABS',
  webDir: 'AppServicios.Api/wwwroot',
  server: {
    url: publicUrl,
    cleartext: false,
    allowNavigation: [
      '*.onrender.com',
      '*.up.railway.app',
      '*.hostinger.com',
      '*.hostingerapp.com'
    ]
  },
  android: {
    allowMixedContent: false,
    captureInput: true
  },
  ios: {
    contentInset: 'always'
  }
};

export default config;
