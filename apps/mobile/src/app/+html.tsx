import { ScrollViewStyleReset } from 'expo-router/html';
import type { PropsWithChildren } from 'react';

import { darkColors, lightColors } from '@/ui/theme';

/**
 * Web-only document shell (never runs on native). Paints the page background in the right scheme before React
 * loads, so dark-mode users never get a white flash or a white overscroll area.
 */
export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="es">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no, viewport-fit=cover" />
        <meta name="description" content="ChambaIA: tu agente personal para encontrar trabajo en Lima." />
        <meta name="theme-color" content={lightColors.bg} media="(prefers-color-scheme: light)" />
        <meta name="theme-color" content={darkColors.bg} media="(prefers-color-scheme: dark)" />
        <ScrollViewStyleReset />
        <style dangerouslySetInnerHTML={{ __html: background }} />
      </head>
      <body>{children}</body>
    </html>
  );
}

const background = `
body { background-color: ${lightColors.bg}; }
@media (prefers-color-scheme: dark) {
  body { background-color: ${darkColors.bg}; }
}`;
