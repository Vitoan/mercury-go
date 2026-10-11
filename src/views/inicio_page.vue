<template>
  <comp-page titulo="MercuryGO" :mostrar_actualizar="true" @actualizar="alActualizar">
    <!-- Banner de Bienvenida -->
    <ion-card style="margin-inline: 0;">
      <ion-card-header>
        <ion-card-title class="mercury-titulo-destacado">Logística en Movimiento</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Bienvenido a MercuryGO. Sistema integral para gestión de pedidos, rutas de carga LIFO y control de stock.
      </ion-card-content>
    </ion-card>

    <!-- Sección de Últimas Ofertas Destacadas -->
    <div v-if="ofertas.length > 0" class="mercury-seccion-ofertas">
      <div class="mercury-ofertas-titulo">
        <ion-icon :icon="flameOutline" color="danger" />
        <h2>🔥 Ofertas Destacadas de la Semana</h2>
      </div>

      <div class="mercury-ofertas-scroll">
        <ion-card v-for="oferta in ofertas" :key="oferta.id" class="mercury-card-oferta">
          <div class="mercury-oferta-img-caja">
            <img
              v-if="oferta.imagen_url"
              :src="resolver_url_imagen(oferta.imagen_url)"
              :alt="oferta.nombre"
              class="mercury-oferta-img"
              @error="oferta.imagen_url = null"
            />
            <div v-else class="mercury-oferta-sin-img">
              <ion-icon :icon="cubeOutline" />
            </div>
            <ion-badge color="danger" class="mercury-badge-oferta-chip">OFERTA</ion-badge>
          </div>

          <ion-card-content class="mercury-oferta-cuerpo">
            <h3 class="mercury-oferta-nombre">{{ oferta.nombre }}</h3>
            <p class="mercury-oferta-desc">{{ oferta.descripcion || 'Precio especial de distribución' }}</p>
            <div class="mercury-oferta-pie">
              <span class="mercury-oferta-precio">${{ oferta.precio?.toLocaleString('es-AR') }}</span>
              <ion-button
                size="small"
                color="primary"
                @click="sumarAlCarrito(oferta)"
                title="Agregar al Carrito"
              >
                <ion-icon slot="icon-only" :icon="cartOutline" />
              </ion-button>
            </div>
          </ion-card-content>
        </ion-card>
      </div>
    </div>

    <ion-card style="margin-inline: 0; margin-top: 16px;">
      <ion-card-header>
        <ion-card-title class="mercury-titulo-destacado">Versión 5 · Autenticación, Roles & Biometría</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Capa de seguridad empresarial y control de acceso activa:
        <ul style="margin: 8px 0 0 16px; padding: 0;">
          <li><strong>Autenticación JWT & Refresh Tokens:</strong> Tokens en memoria con renovación transparente y rotación hasheada (SHA-256).</li>
          <li><strong>Roles en Base de Datos (B2B):</strong> Permisos dinámicos para Administrador, Operario, Chofer y Cliente.</li>
          <li><strong>Revocación Inmediata:</strong> Validación en cada petición (<code>OnTokenValidated</code>) ante bajas o cambios de permisos.</li>
          <li><strong>Seguridad Móvil & Biometría:</strong> Almacenamiento cifrado en Keystore y desbloqueo local por huella digital.</li>
        </ul>
      </ion-card-content>
    </ion-card>
  </comp-page>
</template>

<script setup>
import { computed, onMounted } from 'vue';
import {
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardContent,
  IonBadge,
  IonButton,
  IonIcon
} from '@ionic/vue';
import { flameOutline, cubeOutline, cartOutline } from 'ionicons/icons';
import CompPage from '../components/estructura/comp_page.vue';
import { productos_store } from '@/stores/productos_store';
import { carrito_store } from '@/stores/carrito_store';
import { resolver_url_imagen } from '@/utils/imagenes';
import { vibrar_toque } from '@/services/vibracion_service';

const ofertas = computed(() => productos_store.ofertas);

const sumarAlCarrito = (producto) => {
  vibrar_toque();
  carrito_store.agregar_producto(producto, 1);
};

const alActualizar = async (event) => {
  await productos_store.cargar_resumen(event);
};

onMounted(() => {
  productos_store.cargar_resumen();
});
</script>

<style scoped>
.mercury-seccion-ofertas {
  margin-top: 18px;
  margin-inline: -16px;
  padding: 0 16px;
}

.mercury-ofertas-titulo {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
}

.mercury-ofertas-titulo h2 {
  font-size: 1.05rem;
  font-weight: 700;
  margin: 0;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-ofertas-scroll {
  display: flex;
  overflow-x: auto;
  gap: 12px;
  padding-bottom: 8px;
  scrollbar-width: none;
  -webkit-overflow-scrolling: touch;
}

.mercury-card-oferta {
  flex: 0 0 220px;
  margin: 0;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  display: flex;
  flex-direction: column;
}

.mercury-oferta-img-caja {
  position: relative;
  width: 100%;
  height: 120px;
  background: var(--ion-color-light, #f1f5f9);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.mercury-oferta-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.mercury-oferta-sin-img {
  font-size: 2.5rem;
  color: var(--ion-color-medium, #94a3b8);
}

.mercury-badge-oferta-chip {
  position: absolute;
  top: 8px;
  right: 8px;
  font-size: 0.7rem;
  font-weight: 700;
}

.mercury-oferta-cuerpo {
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  flex: 1;
}

.mercury-oferta-nombre {
  font-size: 0.92rem;
  font-weight: 600;
  margin: 0 0 4px 0;
  color: var(--ion-color-dark, #1e293b);
  line-height: 1.25;
}

.mercury-oferta-desc {
  font-size: 0.78rem;
  color: var(--ion-color-medium, #64748b);
  margin: 0 0 10px 0;
  flex: 1;
}

.mercury-oferta-pie {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.mercury-oferta-precio {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--ion-color-primary, #2563eb);
}
</style>