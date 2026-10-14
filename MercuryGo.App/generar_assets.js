import fs from 'fs';
import path from 'path';
import * as PImage from 'pureimage';

const ASSETS_DIR = path.resolve('assets');
if (!fs.existsSync(ASSETS_DIR)) {
  fs.mkdirSync(ASSETS_DIR, { recursive: true });
}

// Colores oficiales MercuryGO
const COLORS = {
  celeste: '#38BDF8',
  noche: '#0F172A',
  coral: '#FB923C',
  blanco: '#FFFFFF',
  grisClaro: '#F8FAFC'
};

// Dibuja el camión con la "G" y las alas de MercuryGO
function dibujarLogo(ctx, cx, cy, escala, modoOscuro = false) {
  ctx.save();
  ctx.translate(cx, cy);
  ctx.scale(escala, escala);

  const colorCamion = modoOscuro ? COLORS.noche : COLORS.celeste;
  const colorAlas = modoOscuro ? COLORS.coral : COLORS.blanco;

  // 1. Alas
  ctx.fillStyle = colorAlas;
  ctx.beginPath();
  ctx.moveTo(-110, -5);
  ctx.bezierCurveTo(-150, -110, -60, -170, 20, -145);
  ctx.bezierCurveTo(-15, -115, -15, -80, -35, -55);
  ctx.bezierCurveTo(0, -60, 25, -50, 10, -25);
  ctx.bezierCurveTo(-15, -15, -45, -5, -80, 5);
  ctx.closePath();
  ctx.fill();

  // 2. Chasis, flecha frontal y cabina ("G")
  ctx.fillStyle = colorCamion;
  ctx.beginPath();
  ctx.moveTo(-115, 30);
  ctx.lineTo(-75, 5);
  ctx.lineTo(-75, 20);
  ctx.lineTo(-10, 20);
  ctx.lineTo(25, -40);
  ctx.lineTo(85, -40);
  ctx.lineTo(85, 25);
  ctx.lineTo(25, 25);
  ctx.lineTo(25, 5);
  ctx.lineTo(55, 5);
  ctx.lineTo(55, -15);
  ctx.lineTo(35, -15);
  ctx.lineTo(10, 30);
  ctx.lineTo(-75, 30);
  ctx.lineTo(-75, 45);
  ctx.closePath();
  ctx.fill();

  // 3. Ruedas
  ctx.fillStyle = colorCamion;
  ctx.beginPath();
  ctx.arc(-45, 50, 18, 0, Math.PI * 2);
  ctx.arc(45, 50, 18, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = modoOscuro ? COLORS.grisClaro : COLORS.blanco;
  ctx.beginPath();
  ctx.arc(-45, 50, 7, 0, Math.PI * 2);
  ctx.arc(45, 50, 7, 0, Math.PI * 2);
  ctx.fill();

  ctx.restore();
}

async function exportarPNG(img, rutaDestino) {
  const stream = fs.createWriteStream(rutaDestino);
  await PImage.encodePNGToStream(img, stream);
}

async function main() {
  console.log('Generando assets para MercuryGO...');

  // 1. icon-foreground.png (1024x1024 - Transparente, logo centrado en zona segura 66%)
  const iconFg = PImage.make(1024, 1024);
  const ctxFg = iconFg.getContext('2d');
  dibujarLogo(ctxFg, 512, 512, 2.2, false);
  await exportarPNG(iconFg, path.join(ASSETS_DIR, 'icon-foreground.png'));
  console.log('✓ assets/icon-foreground.png creado');

  // 2. icon-background.png (1024x1024 - Fondo sólido)
  const iconBg = PImage.make(1024, 1024);
  const ctxBg = iconBg.getContext('2d');
  ctxBg.fillStyle = COLORS.noche;
  ctxBg.fillRect(0, 0, 1024, 1024);
  await exportarPNG(iconBg, path.join(ASSETS_DIR, 'icon-background.png'));
  console.log('✓ assets/icon-background.png creado');

  // 3. icon-only.png (1024x1024 - Icono con insignia circular)
  const iconOnly = PImage.make(1024, 1024);
  const ctxOnly = iconOnly.getContext('2d');
  ctxOnly.fillStyle = COLORS.grisClaro;
  ctxOnly.beginPath();
  ctxOnly.arc(512, 512, 480, 0, Math.PI * 2);
  ctxOnly.fill();
  ctxOnly.lineWidth = 24;
  ctxOnly.strokeStyle = COLORS.noche;
  ctxOnly.stroke();
  dibujarLogo(ctxOnly, 512, 530, 2.3, false);
  await exportarPNG(iconOnly, path.join(ASSETS_DIR, 'icon-only.png'));
  console.log('✓ assets/icon-only.png creado');

  // 4. splash.png (2732x2732 - Modo Claro)
  const splash = PImage.make(2732, 2732);
  const ctxSp = splash.getContext('2d');
  ctxSp.fillStyle = COLORS.grisClaro;
  ctxSp.fillRect(0, 0, 2732, 2732);
  ctxSp.fillStyle = COLORS.blanco;
  ctxSp.beginPath();
  ctxSp.arc(1366, 1366, 420, 0, Math.PI * 2);
  ctxSp.fill();
  ctxSp.lineWidth = 16;
  ctxSp.strokeStyle = COLORS.noche;
  ctxSp.stroke();
  dibujarLogo(ctxSp, 1366, 1380, 2.0, false);
  await exportarPNG(splash, path.join(ASSETS_DIR, 'splash.png'));
  console.log('✓ assets/splash.png creado');

  // 5. splash-dark.png (2732x2732 - Modo Oscuro)
  const splashDark = PImage.make(2732, 2732);
  const ctxSpDark = splashDark.getContext('2d');
  ctxSpDark.fillStyle = COLORS.noche;
  ctxSpDark.fillRect(0, 0, 2732, 2732);
  ctxSpDark.fillStyle = COLORS.grisClaro;
  ctxSpDark.beginPath();
  ctxSpDark.arc(1366, 1366, 420, 0, Math.PI * 2);
  ctxSpDark.fill();
  ctxSpDark.lineWidth = 16;
  ctxSpDark.strokeStyle = COLORS.celeste;
  ctxSpDark.stroke();
  dibujarLogo(ctxSpDark, 1366, 1380, 2.0, true);
  await exportarPNG(splashDark, path.join(ASSETS_DIR, 'splash-dark.png'));
  console.log('✓ assets/splash-dark.png creado');

  console.log('\n✨ Todos los assets fueron creados correctamente en la carpeta assets/.');
}

main();