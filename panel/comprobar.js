'use strict';
// Comprobación rápida de que el panel puede funcionar en este equipo: versión de Node y piezas nativas.
// La lanza el plug-in una vez por sesión de PL/SQL Developer antes de abrir Claude a través del panel; si falla,
// abre Claude como siempre (sin panel) y deja el motivo en plugin.log.
const [mayor] = process.versions.node.split('.').map(Number);
if (mayor < 18) {
  console.error(`Node.js ${process.versions.node} es demasiado antiguo: el panel necesita la versión 18 o superior.`);
  process.exit(1);
}
try {
  require('@lydell/node-pty');
  require('ws');
  require('@xterm/headless');
  require('@xterm/addon-serialize');
  require('@xterm/addon-unicode11');
} catch (e) {
  console.error(`Falta una pieza del panel: ${e.message.split('\n')[0]}`);
  process.exit(1);
}
console.log(`ok ${require('./package.json').version} node ${process.versions.node}`);
