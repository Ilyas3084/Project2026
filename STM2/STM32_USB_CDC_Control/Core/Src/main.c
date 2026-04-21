/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body - USB CDC VCP для управления 10 SW + 2 KEY
  *                   Логика управления вынесена из usbd_cdc_if.c в main.c
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "usb_device.h"
#include "usbd_cdc_if.h"
#include <stdio.h>
#include <string.h>

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */
#define APP_RX_DATA_SIZE  64U
/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */
// Внешние переменные из usbd_cdc_if.c
extern volatile uint8_t usb_data_ready;
extern uint8_t usb_rx_buffer[APP_RX_DATA_SIZE];
extern uint32_t usb_rx_len;
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
static void Process_USB_Command(uint8_t* buf, uint32_t len);
static void Set_Outputs(uint32_t value);
static void Send_Status(void);
/* USER CODE BEGIN PFP */

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{
  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_USB_DEVICE_Init();

  /* USER CODE BEGIN 2 */
  // Установка начального состояния всех выходов в 0
  Set_Outputs(0);

  // Отправка приветственного сообщения при старте
  CDC_Transmit_FS((uint8_t*)"STM32 CDC Ready\r\nSend number 0-4095 or ? or status\r\n", 50);
  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
    // Проверяем флаг получения новых данных от USB
    if (usb_data_ready)
    {
      usb_data_ready = 0;  // Сбрасываем флаг
      Process_USB_Command(usb_rx_buffer, usb_rx_len);
    }

    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
    HAL_Delay(10);  // Небольшая задержка для стабильности
  }
  /* USER CODE END 3 */
}

/**
  * @brief  Обработка команд, полученных через USB
  * @param  buf: указатель на буфер с данными
  * @param  len: длина данных
  */
static void Process_USB_Command(uint8_t* buf, uint32_t len)
{
  uint32_t value = 0;

  // Защита от переполнения и добавление null-терминатора
  if (len >= APP_RX_DATA_SIZE)
    len = APP_RX_DATA_SIZE - 1;
  buf[len] = '\0';

  // Удаляем символы новой строки в конце
  while (len > 0 && (buf[len-1] == '\r' || buf[len-1] == '\n'))
  {
    buf[len-1] = '\0';
    len--;
  }

  // Если строка пустая, игнорируем
  if (len == 0)
    return;

  // Пробуем распарсить число
  if (sscanf((char*)buf, "%lu", &value) == 1)
  {
    if (value <= 4095)   // 0…4095 (12 бит)
    {
      Set_Outputs(value);
      char resp[32];
      snprintf(resp, sizeof(resp), "OK %lu\r\n", value);
      CDC_Transmit_FS((uint8_t*)resp, strlen(resp));
    }
    else
    {
      CDC_Transmit_FS((uint8_t*)"ERR: value > 4095\r\n", 20);
    }
  }
  // Команда "?" или "status" - чтение состояния
  else if (strcmp((char*)buf, "?") == 0 || strcmp((char*)buf, "status") == 0)
  {
    Send_Status();
  }
  // Неизвестная команда
  else
  {
    CDC_Transmit_FS((uint8_t*)"Send number 0-4095 or ? or status\r\n", 36);
  }
}

/**
  * @brief  Установка 12 выходов по битовой маске
  * @param  value: 12-битное значение (биты 0-11)
  */
static void Set_Outputs(uint32_t value)
{
  // SW0 .. SW3 (PD15, PD13, PD11, PD9)
  HAL_GPIO_WritePin(GPIOD, GPIO_PIN_15, (value & (1UL << 0)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOD, GPIO_PIN_13, (value & (1UL << 1)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOD, GPIO_PIN_11, (value & (1UL << 2)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOD, GPIO_PIN_9,  (value & (1UL << 3)) ? GPIO_PIN_SET : GPIO_PIN_RESET);

  // SW4 .. SW6 (PB15, PB13, PB11)
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_15, (value & (1UL << 4)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_13, (value & (1UL << 5)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_11, (value & (1UL << 6)) ? GPIO_PIN_SET : GPIO_PIN_RESET);

  // SW7 .. SW9 (PE15, PE13, PE11)
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_15, (value & (1UL << 7)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_13, (value & (1UL << 8)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_11, (value & (1UL << 9)) ? GPIO_PIN_SET : GPIO_PIN_RESET);

  // KEY0 и KEY1 (PE9, PE7)
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_9,  (value & (1UL << 10)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_7,  (value & (1UL << 11)) ? GPIO_PIN_SET : GPIO_PIN_RESET);
}

/**
  * @brief  Чтение состояния всех выходов и отправка через USB
  */
static void Send_Status(void)
{
  uint32_t state = 0;

  // Читаем SW0 .. SW3
  if (HAL_GPIO_ReadPin(GPIOD, GPIO_PIN_15) == GPIO_PIN_SET) state |= (1UL << 0);
  if (HAL_GPIO_ReadPin(GPIOD, GPIO_PIN_13) == GPIO_PIN_SET) state |= (1UL << 1);
  if (HAL_GPIO_ReadPin(GPIOD, GPIO_PIN_11) == GPIO_PIN_SET) state |= (1UL << 2);
  if (HAL_GPIO_ReadPin(GPIOD, GPIO_PIN_9)  == GPIO_PIN_SET) state |= (1UL << 3);

  // Читаем SW4 .. SW6
  if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_15) == GPIO_PIN_SET) state |= (1UL << 4);
  if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_13) == GPIO_PIN_SET) state |= (1UL << 5);
  if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_11) == GPIO_PIN_SET) state |= (1UL << 6);

  // Читаем SW7 .. SW9
  if (HAL_GPIO_ReadPin(GPIOE, GPIO_PIN_15) == GPIO_PIN_SET) state |= (1UL << 7);
  if (HAL_GPIO_ReadPin(GPIOE, GPIO_PIN_13) == GPIO_PIN_SET) state |= (1UL << 8);
  if (HAL_GPIO_ReadPin(GPIOE, GPIO_PIN_11) == GPIO_PIN_SET) state |= (1UL << 9);

  // Читаем KEY0, KEY1
  if (HAL_GPIO_ReadPin(GPIOE, GPIO_PIN_9)  == GPIO_PIN_SET) state |= (1UL << 10);
  if (HAL_GPIO_ReadPin(GPIOE, GPIO_PIN_7)  == GPIO_PIN_SET) state |= (1UL << 11);

  char resp[48];
  snprintf(resp, sizeof(resp), "STATE %lu\r\n", state);
  CDC_Transmit_FS((uint8_t*)resp, strlen(resp));
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE1);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.HSICalibrationValue = RCC_HSICALIBRATION_DEFAULT;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSI;
  RCC_OscInitStruct.PLL.PLLM = 16;
  RCC_OscInitStruct.PLL.PLLN = 192;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV2;
  RCC_OscInitStruct.PLL.PLLQ = 4;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_HSI;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV1;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_0) != HAL_OK)
  {
    Error_Handler();
  }
}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOE_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOD_CLK_ENABLE();

  /* Все пины на выход, начальное состояние — выключено (0) */
  HAL_GPIO_WritePin(GPIOD, GPIO_PIN_9|GPIO_PIN_11|GPIO_PIN_13|GPIO_PIN_15, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_11|GPIO_PIN_13|GPIO_PIN_15, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOE, GPIO_PIN_7|GPIO_PIN_9|GPIO_PIN_11|GPIO_PIN_13|GPIO_PIN_15, GPIO_PIN_RESET);

  /* Настройка всех 12 пинов как выходы */
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;

  // PD9, PD11, PD13, PD15
  GPIO_InitStruct.Pin = GPIO_PIN_9 | GPIO_PIN_11 | GPIO_PIN_13 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);

  // PB11, PB13, PB15
  GPIO_InitStruct.Pin = GPIO_PIN_11 | GPIO_PIN_13 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  // PE7, PE9, PE11, PE13, PE15
  GPIO_InitStruct.Pin = GPIO_PIN_7 | GPIO_PIN_9 | GPIO_PIN_11 | GPIO_PIN_13 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOE, &GPIO_InitStruct);
}

/* USER CODE BEGIN 4 */

/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
