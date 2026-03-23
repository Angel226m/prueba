# 🧪 GUÍA COMPLETA DE PRUEBA - SISTEMA DE SESIÓN DE 1 MINUTO

## 📋 PASO 1: EJECUTAR LA APLICACIÓN

```bash
cd c:\Users\angel\Desktop\ceplan
dotnet run
```

La aplicación se ejecutará en: **https://localhost:5001** o **http://localhost:5000**

## 📋 PASO 2: LOGIN

- **URL:** https://localhost:5001
- **Usuario:** `Angel`
- **Contraseña:** `Angel2206!`

## 📋 PASO 3: ABRIR HERRAMIENTAS DE DEBUGGING

1. **Presiona F12** para abrir las Developer Tools
2. Ve a la pestaña **"Console"** (Consola)
3. Deberías ver mensajes como:
   ```
   [CEPLAN] ✓ Valores configurados: {WARN_SEC: 30, TIMEOUT_MIN: 1, inactivityMs: 60000}
   [CEPLAN] 🚀 INICIANDO SISTEMA DE SESIÓN COMPLETO
   [CEPLAN] 🎯 Configurando detectores de actividad...
   [CEPLAN] ✓ Event listener agregado: mousedown
   [CEPLAN] ✓ Event listener agregado: mousemove
   [CEPLAN] ✓ Event listener agregado: keydown
   [CEPLAN] ✓ Event listener agregado: scroll
   [CEPLAN] ✓ Event listener agregado: touchstart
   [CEPLAN] ✓ Event listener agregado: click
   [CEPLAN] ✅ Todos los detectores de actividad configurados
   [CEPLAN] ✓ Iniciando timer de sesión...
   [CEPLAN] ✓ Timer configurado para: 60 segundos
   [CEPLAN] ✅ Sistema de sesión inicializado
   ```

## 📋 PASO 4: PRUEBA MANUAL DEL MODAL

En la **consola del navegador**, escribe:
```javascript
testModal()
```

Esto debería mostrar el modal inmediatamente. Si no aparece, hay un problema con el HTML del modal.

## 📋 PASO 5: PRUEBA DE INACTIVIDAD REAL

1. **NO TOQUES NADA** (no muevas el ratón, no hagas scroll, no toques teclas)
2. **Espera exactamente 1 minuto**
3. En la consola deberías ver:
   ```
   [CEPLAN] ⚠️ TIMEOUT ALCANZADO - Mostrando modal
   [CEPLAN] ⚠️ MOSTRANDO MODAL DE INACTIVIDAD
   [CEPLAN] Modal element: <div class="modal-overlay" id="modalSesion">
   [CEPLAN] ✓ Modal mostrado
   [CEPLAN] ⏰ Countdown: 29 segundos restantes
   [CEPLAN] ⏰ Countdown: 28 segundos restantes
   ...
   ```

## 📋 PASO 6: PRUEBA DE ACTIVIDAD

1. **Mueve el ratón o haz scroll cada 30-40 segundos**
2. En la consola deberías ver:
   ```
   [CEPLAN] ✓ Actividad detectada en: [hora]
   [CEPLAN] ✓ Iniciando timer de sesión...
   [CEPLAN] ✓ Timer configurado para: 60 segundos
   ```
3. **El modal NUNCA debería aparecer** mientras sigas siendo activo

## 📋 PASO 7: PRUEBA DE EXTENSIÓN DE SESIÓN

1. Espera a que aparezca el modal (1 minuto sin actividad)
2. **Haz clic en "Extender sesión"**
3. En la consola deberías ver:
   ```
   [CEPLAN] 🔄 Usuario extendiendo sesión...
   [CEPLAN] ✅ Sesión extendida exitosamente
   [CEPLAN] ✓ Iniciando timer de sesión...
   ```

## 🚨 PROBLEMAS COMUNES Y SOLUCIONES

### ❌ "Modal no encontrado en el DOM"
**Problema:** El HTML del modal no está presente
**Solución:** Verifica que el archivo `Views/Shared/_Layout.cshtml` contiene el modal HTML

### ❌ No se ven mensajes en la consola
**Problema:** El JavaScript no se está ejecutando
**Solución:** Verifica que no hay errores de JavaScript en la consola

### ❌ El timer no se resetea con actividad
**Problema:** Los event listeners no están funcionando
**Solución:** Verifica en la consola que se muestran todos los "Event listener agregado"

### ❌ "Error extendiendo sesión"
**Problema:** El endpoint `/Account/ExtenderSesion` no responde
**Solución:** Verifica que el método `ExtenderSesion` esté en `AccountController.cs`

## 🎯 VALORES ESPERADOS

- **TIMEOUT_MIN:** 1 (minuto de inactividad)
- **WARN_SEC:** 30 (segundos de advertencia)
- **inactivityMs:** 60000 (60 segundos en milisegundos)

## 👀 QUÉ BUSCAR EN LA CONSOLA

✅ **FUNCIONANDO CORRECTAMENTE:**
- Se ven todos los mensajes de inicialización
- Los event listeners se registran correctamente
- El timer se resetea con actividad
- El modal aparece después de 1 minuto exacto de inactividad
- El countdown funciona correctamente

❌ **NO FUNCIONANDO:**
- No se ven mensajes en la consola
- Error: "Modal no encontrado"
- El timer no se resetea con actividad
- El modal no aparece después de 1 minuto
- Error al extender la sesión

---

**Si algo no funciona, copia exactamente los mensajes de error de la consola para poder diagnosticar el problema.**