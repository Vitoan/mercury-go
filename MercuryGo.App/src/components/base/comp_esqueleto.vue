<template>
  <ion-list>
    <ion-item v-for="n in 6" :key="n">
      <ion-label>
        <h3><ion-skeleton-text :animated="true" style="width: 60%; line-height: 1.25em;"></ion-skeleton-text></h3>
        <p><ion-skeleton-text :animated="true" style="width: 40%; line-height: 1em;"></ion-skeleton-text></p>
        <p><ion-skeleton-text :animated="true" style="width: 25%; line-height: 0.9em;"></ion-skeleton-text></p>
      </ion-label>
    </ion-item>
  </ion-list>
</template>

<script setup>
import { IonList, IonItem, IonLabel, IonSkeletonText } from '@ionic/vue';
</script>