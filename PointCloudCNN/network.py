import argparse
import re
import cv2
import os, glob
import pandas as pd
import matplotlib.pyplot as plt
import math
import odl.contrib.tensorflow
import random
import keras.backend as K
import keras.losses
from keras.layers import Input, Conv2D, BatchNormalization, Activation, Concatenate, MaxPool2D, Flatten, Dense
from keras.models import Model, Sequential, load_model
from keras.callbacks import CSVLogger, ModelCheckpoint, LearningRateScheduler, History
from keras.optimizers import Adam
from keras.utils import plot_model
from skimage.io import imread
import numpy as np

# Hyper parameter args
parser = argparse.ArgumentParser()

# Hyperparameters
parser.add_argument('--batch_size', default=64, type=int, help='batch size')
parser.add_argument('--scale_levels', default=5, type=int, help='nr. of scales in textures')
parser.add_argument('--M', default=33, type=int, help='noise level')
parser.add_argument('--epoch', default=50, type=int, help='number of train epochs')
parser.add_argument('--epoch_steps', default=100, type=int, help='steps per epoch')
parser.add_argument('--lr', default=1e-3, type=float, help='initial learning rate for Adam')
parser.add_argument('--dir_name', default="RockCloud", type=str, help='initial learning rate for Adam')

# #reshape data to fit model
# X_train = X_train.reshape(60000, M, M, 1)
# X_test = X_test.reshape(10000, M, M, 1)
#
# #one-hot encode target column
# y_train = to_categorical(y_train)
# y_test = to_categorical(y_test)


#create model


# model.compile(optimizer='adam', loss='categorical_crossentropy', metrics=['accuracy'])
# model.fit(X_train, y_train, validation_data=(X_test, y_test), epochs=50)
args = parser.parse_args()


# Create our ResNet model with all layers using current hyperparameters
def cnn_model():
    model = Sequential()

    # add model layers
    model.add(Conv2D(50, kernel_size=3, activation='relu', input_shape=(args.M, args.M, args.scale_levels)))
    model.add(Conv2D(50, kernel_size=3, activation='relu'))

    model.add(MaxPool2D(pool_size=(2, 2)))

    model.add(Conv2D(96, kernel_size=3, activation='relu'))
    model.add(MaxPool2D(pool_size=(2, 2)))

    model.add(Flatten())  # 3456 neurons at this point
    model.add(Dense(2048, activation='relu'))
    model.add(Dense(1024, activation='relu'))
    model.add(Dense(512, activation='relu'))

    # Predict normal XY, roughness
    model.add(Dense(3))

    return model


# Step schedule learning rate
def lr_schedule(epoch):
    drop = 0.5
    epochs_drop = 5
    return args.lr * math.pow(drop, math.floor((1 + epoch) / epochs_drop))


# Return one batch of data at a time for Keras
def train_datagen():
    index = 0
    dir = './TrainingData/' + args.dir_name + '/'
    files = os.listdir(dir)
    files.sort()

    while True:
        batch_x = np.empty(shape=(args.batch_size, args.M, args.M, args.scale_levels))
        batch_y = np.empty(shape=(args.batch_size, 3))

        for i in range(args.batch_size):
            for k in range(args.scale_levels):
                f = files[index]

                k_index = f.index('_k_')
                x_index = f.index('_x_')
                y_index = f.index('_y_')
                r_index = f.index('_r_')

                ext_index = f.index('.png')

                k_test = int(f[k_index + 3:x_index])

                if k != k_test:
                    print("Something went wrong when generating batches! Are your files in the right order?" + k + ", " + k_test)

                normalX = float(f[x_index + 3:y_index])
                normalY = float(f[y_index + 3:r_index])

                roughness = float(f[r_index + 3:ext_index])

                image = imread(dir + f)

                batch_x[i, :, :, k] = np.reshape(image[:, :, 0], (args.M, args.M))
                batch_y[i, :] = np.array([normalX, normalY, roughness])

                index += 1

            index = index % len(files)

        yield batch_x, batch_y


        # batch_y = np.empty(shape=(args.batch_size, image_size, image_size, 1))
        # batch_y_bf = np.empty(shape=(args.batch_size, radon_size[0], radon_size[1], 1))
        # batch_x = np.empty(shape=(args.batch_size, image_size, image_size, 1))

        # for i in range(args.batch_size):
        #     gt, bf, noisy = create_training_data(args.sigma)
        #
        #     # Remap to [-1, 1] for better FP accuracy
        #     batch_y[i, :, :, 0] = noisy
        #     batch_y_bf[i, :, :, 0] = bf
        #
        #     batch_x[i, :, :, 0] = gt
        # yield [batch_y, batch_y_bf], batch_x



# FInd last epoch saved in a folder
def get_last_epoch(save_dir: str):
    # load the last model if it exists
    file_list = glob.glob(os.path.join(save_dir, 'model_*.hdf5'))  # get name list of all .hdf5 files

    if file_list:
        epochs_exist = []
        for file_ in file_list:
            result = re.findall(".*model_(.*).hdf5.*", file_)
            epochs_exist.append(int(result[0]))

        return max(epochs_exist)

    return 0


# Load model at specific or last epoch
def load_last_model(save_dir, load_epoch=-1):
    if load_epoch == -1:
        load_epoch = get_last_epoch(save_dir)

    return load_model(os.path.join(save_dir, 'model_%03d.hdf5' % load_epoch), compile=False)


# Main training routine
def train_model():
    # Setup checkpoint folder
    save_dir = os.path.join('./models/', args.dir_name)
    os.makedirs(save_dir, mode=0o777, exist_ok=True)

    initial_epoch = get_last_epoch(save_dir)

    # Resume training if we have a checkpoint
    if initial_epoch > 0:
        print('resuming by loading epoch %03d' % initial_epoch)
        model = load_last_model(save_dir)

        # Get some data
        x_batch, y_batch = train_datagen().__next__()

        plt.imshow(x_batch[0, :, :, 0])
        plt.show()

        # And see the prediction!
        pred_norm = model.predict(x_batch[:, :, :, :])

        print(y_batch)
        print(pred_norm)

    else:
        # Create fresh model
        model = cnn_model()

    model.compile(
        optimizer=Adam(lr=args.lr, decay=0.0),  # Optimize with Adam. Learning rate is set by schedule
        loss=lambda y_true, y_pred: keras.losses.mean_squared_error(y_true, y_pred)  # L2 loss
    )

    # Save out model occasionally
    checkpointer = ModelCheckpoint(os.path.join(save_dir, 'model_{epoch:03d}.hdf5'), verbose=1, save_weights_only=False, period=1)

    # Keep loss log
    csv_logger = CSVLogger(os.path.join(save_dir, 'log.csv'), append=True, separator=',')
    lr_scheduler = LearningRateScheduler(lr_schedule)

    # Train model
    model.fit_generator(train_datagen(),
                        steps_per_epoch=args.epoch_steps,
                        epochs=args.epoch,
                        verbose=1,
                        initial_epoch=initial_epoch,
                        callbacks=[checkpointer, csv_logger, lr_scheduler]
                        )



# ###################################
# # Some visualizations for the report
# def model_loss_validation_curve():
#     save_dir = "./models/cnn_post_depth12_sigma100"
#
#     # Read training losses with pandas
#     train = pd.read_csv(os.path.join(save_dir, 'log.csv')).values
#
#     # Manually calculate validation losses by loading all models
#     validation_loss = []
#     file_list = glob.glob(os.path.join(save_dir, 'model_*.hdf5'))  # get name list of all .hdf5 files
#
#     if file_list:
#         for file in file_list:
#             model = load_model(file, custom_objects=cnn_model_config(), compile=False)
#
#             l1 = 0
#             # Generate L1 loss for entire batch
#             for i in range(args.batch_size):
#                 f_true, g, noisy = create_training_data(1.0)
#                 f_rec = radon_reconstruct_model(g, train_projector, model)
#                 l1 += np.sum(np.abs(f_true - f_rec))
#
#             validation_loss.append(l1)
#             print(file + " l1 loss: " + str(l1))
#
#     # Plot
#     train_losses = train[:, 1]
#     plt.plot(train_losses)
#
#     plt.plot(validation_loss)
#     plt.legend(["Training", "Validation"])
#     plt.ylabel("Loss")
#     plt.xlabel("Epoch")
#     plt.show()



if __name__ == '__main__':
    # Train model with current hyperparameters
    train_model()