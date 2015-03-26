﻿namespace Gear.Properties {
    
    
    // Esta clase le permite controlar eventos específicos en la clase de configuración:
    //  El evento SettingChanging se desencadena antes de cambiar un valor de configuración.
    //  El evento PropertyChanged se desencadena después de cambiar el valor de configuración.
    //  El evento SettingsLoaded se desencadena después de cargar los valores de configuración.
    //  El evento SettingsSaving se desencadena antes de guardar los valores de configuración.
    internal sealed partial class Settings {
        
        public Settings() {
            // // Para agregar los controladores de eventos para guardar y cambiar la configuración, quite la marca de comentario de las líneas:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e) {
            // Agregar código para administrar aquí el evento SettingChangingEvent.
        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // Agregar código para administrar aquí el evento SettingsSaving.
        }

        //This DOXIGEN documentation goes here, because Settings.Designer.cs is auto generated by MSVS 
        //and the documentation is lost if put there.
        //
        /// @property LastPlugin
        /// @brief Last plugin succesfully opened or saved.
        /// @details Include complete path and name.
        /// @version V15.03.26 - Introduced in a User property of the program.

        /// @property LastBinary
        /// @brief Last binary file succesfully opened.
        /// @details Include complete path and name.
        /// @version V15.03.26 - Introduced in a User property of the program.

        /// @property UseNoTemplate
        /// @brief Indicates to use the default template on the creation of a new plugin (=false), or to 
        /// do not use a template (blank content intially) (=true).
        /// @version V15.03.26 - Added in a User property of the program.

        /// @property UpdateEachSteps
        /// @brief Number of steps before update the windows and tabs.
        /// @version V15.03.26 - Added in a User property of the program.

    }
}
